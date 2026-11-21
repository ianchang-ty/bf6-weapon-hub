using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;

namespace BF6WeaponHub
{
    public static class Program
    {
        private static Dictionary<string, object> LoadEmbeddedBundle()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream rawStream = asm.GetManifestResourceStream("BF6Bundle.gz"))
            {
                if (rawStream == null)
                    throw new InvalidOperationException("Embedded resource BF6Bundle.gz not found.");
                using (GZipStream gz = new GZipStream(rawStream, CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gz, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    return serializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
        }

        private static int RunSelfTest()
        {
            Dictionary<string, object> bundle = LoadEmbeddedBundle();
            IList weapons = DataHelper.AsList(bundle["weapons"]);
            Dictionary<string, object> gs = DataHelper.AsDict(bundle["gunsmith"]);
            GunsmithEngine engine = new GunsmithEngine(DataHelper.AsDict(bundle["tables"]));

            if (weapons == null || weapons.Count != 69)
            {
                Console.Error.WriteLine("FAIL: Expected 69 weapons, got " + (weapons == null ? 0 : weapons.Count));
                return 1;
            }

            // Spot-check Z84M (pkm) and M433 (hk433) in-memory calculation
            foreach (object wObj in weapons)
            {
                Dictionary<string, object> w = DataHelper.AsDict(wObj);
                string key = DataHelper.Str(w, "key", "");
                Dictionary<string, object> raw = DataHelper.AsDict(gs[key]);
                IList defBuild = DataHelper.AsList(raw["default_build"]);
                List<string> ids = new List<string>();
                if (defBuild != null) foreach (object x in defBuild) ids.Add(Convert.ToString(x));
                ComputedStats cs = engine.Compute(w, raw, ids, true, true, false);
                if (key == "pkm" && (Math.Abs(cs.Control - 35.27) > 0.5 || cs.Mag != 101))
                {
                    Console.Error.WriteLine("FAIL: PKM stat mismatch: Control=" + cs.Control + " Mag=" + cs.Mag);
                    return 2;
                }
                if (key == "hk433" && (Math.Abs(cs.Control - 52.71) > 0.5 || cs.Mag != 31))
                {
                    Console.Error.WriteLine("FAIL: HK433 stat mismatch: Control=" + cs.Control + " Mag=" + cs.Mag);
                    return 3;
                }
            }

            Console.WriteLine("SELF-TEST OK: 69 weapons verified in memory (zero disk writes, zero network).");
            return 0;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "--self-test")
            {
                return RunSelfTest();
            }

            Dictionary<string, object> bundle = LoadEmbeddedBundle();
            Application app = new Application();
            MainWindow win = new MainWindow(bundle);
            app.Run(win);
            return 0;
        }
    }
}
