using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace BF6WeaponHub
{
    public class ComputedStats
    {
        public double Control;
        public double? Precision;
        public double? Hipfire;
        public double? Mobility;
        public double AdsTime;
        public double V0;
        public int Mag;
        public double ReloadTac;
        public double Rpm;
        public double CycleMs;
        public string FireMode;
        public double Sway;
        public int FireSprint;
        public List<double[]> DmgCurve;
        public Dictionary<string, double> Mult;
        public int Pellets;
        public int TotalCost;
    }

    public class DmgSegment
    {
        public int From;
        public int? To;
        public int Btk;
        public int TtkMs;
    }

    public static class DataHelper
    {
        public static Dictionary<string, object> AsDict(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static IList AsList(object o)
        {
            return o as IList;
        }

        public static string Str(Dictionary<string, object> d, string k, string defVal)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) return defVal;
            return Convert.ToString(d[k], CultureInfo.InvariantCulture);
        }

        public static double Dbl(object v, double defVal)
        {
            if (v == null) return defVal;
            if (v is double) return (double)v;
            if (v is int) return (int)v;
            if (v is long) return (long)v;
            if (v is decimal) return (double)(decimal)v;
            if (v is float) return (float)v;
            double parsed;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return defVal;
        }

        public static double DblKey(Dictionary<string, object> d, string k, double defVal)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) return defVal;
            return Dbl(d[k], defVal);
        }

        public static int IntKey(Dictionary<string, object> d, string k, int defVal)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) return defVal;
            return (int)Math.Round(Dbl(d[k], defVal));
        }

        public static bool BoolKey(Dictionary<string, object> d, string k)
        {
            if (d == null || !d.ContainsKey(k) || d[k] == null) return false;
            if (d[k] is bool) return (bool)d[k];
            return Dbl(d[k], 0) != 0;
        }

        public static double[] DoubleArray(object o)
        {
            IList list = o as IList;
            if (list == null) return new double[0];
            double[] arr = new double[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = Dbl(list[i], 0);
            return arr;
        }

        public static List<double[]> DmgPairs(object o)
        {
            List<double[]> res = new List<double[]>();
            IList list = o as IList;
            if (list == null) return res;
            foreach (object rowObj in list)
            {
                IList r = rowObj as IList;
                if (r != null && r.Count >= 2)
                    res.Add(new double[] { Dbl(r[0], 0), Dbl(r[1], 0) });
            }
            return res;
        }
    }

    public class GunsmithEngine
    {
        private readonly Dictionary<string, double[]> _tblArrays = new Dictionary<string, double[]>();
        private readonly Dictionary<string, int> _dtaLengths = new Dictionary<string, int>();
        private const double EPS = 1e-9;

        public GunsmithEngine(Dictionary<string, object> tables)
        {
            string[] scalarTables = new string[] { "hda_s", "hda_m", "zda", "ida", "msa", "ssa", "hs", "pen", "hipfire" };
            foreach (string name in scalarTables)
            {
                if (tables.ContainsKey(name))
                    _tblArrays[name] = DataHelper.DoubleArray(tables[name]);
            }
            foreach (string dname in new string[] { "dta_weapons", "dta_sidearms" })
            {
                IList l = tables.ContainsKey(dname) ? DataHelper.AsList(tables[dname]) : null;
                _dtaLengths[dname] = l != null ? l.Count : 12;
            }
        }

        private static int ClampIdx(int e, int len)
        {
            if (len <= 0) return 0;
            if (e < 0) return 0;
            if (e >= len) return len - 1;
            return e;
        }

        public static double InterpDmg(List<double[]> dmg, double dist)
        {
            if (dmg == null || dmg.Count == 0) return 0;
            if (dist <= dmg[0][0]) return dmg[0][1];
            for (int s = 0; s < dmg.Count - 1; s++)
            {
                double[] t = dmg[s];
                double[] nx = dmg[s + 1];
                if (dist >= t[0] && dist < nx[0])
                {
                    if (Math.Abs(nx[0] - t[0]) < 1e-9) return nx[1];
                    return t[1] + (nx[1] - t[1]) * (dist - t[0]) / (nx[0] - t[0]);
                }
            }
            return dmg[dmg.Count - 1][1];
        }

        public ComputedStats Compute(
            Dictionary<string, object> wSummary,
            Dictionary<string, object> wRaw,
            List<string> equippedIds,
            bool laserOn,
            bool lightOn,
            bool bipodOn)
        {
            Dictionary<string, object> baseT = DataHelper.AsDict(wRaw["t"]);
            Dictionary<string, object> modsMap = DataHelper.AsDict(wRaw["mods"]);
            IList slotsList = DataHelper.AsList(wRaw["slots"]);

            Dictionary<string, string> slotOf = new Dictionary<string, string>();
            Dictionary<string, int> costOf = new Dictionary<string, int>();
            if (slotsList != null)
            {
                foreach (object sObj in slotsList)
                {
                    Dictionary<string, object> sDict = DataHelper.AsDict(sObj);
                    string sid = DataHelper.Str(sDict, "id", "");
                    IList opts = DataHelper.AsList(sDict["opts"]);
                    if (opts == null) continue;
                    foreach (object oObj in opts)
                    {
                        Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                        string oid = DataHelper.Str(oDict, "id", "");
                        slotOf[oid] = sid;
                        costOf[oid] = DataHelper.IntKey(oDict, "cost", 0);
                    }
                }
            }

            Dictionary<string, int> tiers = new Dictionary<string, int>();
            Dictionary<string, double> muls = new Dictionary<string, double>();
            Dictionary<string, object> sets = new Dictionary<string, object>();
            Dictionary<string, double> adds = new Dictionary<string, double>();
            int ammoHipDelta = 0;
            int totalCost = 0;

            string fireOverride = null;

            foreach (string aid in equippedIds)
            {
                if (string.IsNullOrEmpty(aid)) continue;
                if (costOf.ContainsKey(aid)) totalCost += costOf[aid];
                if (modsMap == null || !modsMap.ContainsKey(aid)) continue;
                IList modList = DataHelper.AsList(modsMap[aid]);
                if (modList == null) continue;

                foreach (object mObj in modList)
                {
                    IList m = DataHelper.AsList(mObj);
                    if (m == null || m.Count < 4) continue;
                    string prop = Convert.ToString(m[0], CultureInfo.InvariantCulture);
                    string op = Convert.ToString(m[1], CultureInfo.InvariantCulture);
                    object valObj = m[2];
                    string cond = Convert.ToString(m[3], CultureInfo.InvariantCulture);

                    if (!string.IsNullOrEmpty(cond))
                    {
                        if (cond.Contains("激光") && !laserOn) continue;
                        if (cond.Contains("灯光") && !lightOn) continue;
                        if (cond.Contains("脚架") && !bipodOn) continue;
                    }

                    if (prop.Contains("射击模式") && valObj is string)
                    {
                        string sv = (string)valObj;
                        if (sv.StartsWith("全自动")) fireOverride = "全自动";
                        continue;
                    }

                    if (op == "+档")
                    {
                        int dv = (int)Math.Round(DataHelper.Dbl(valObj, 0));
                        tiers[prop] = (tiers.ContainsKey(prop) ? tiers[prop] : 0) + dv;
                        if (slotOf.ContainsKey(aid) && slotOf[aid] == "ammo" && prop == "UnzoomedMinAnglesArrayIndex")
                            ammoHipDelta += dv;
                    }
                    else if (op == "×")
                    {
                        double mv = DataHelper.Dbl(valObj, 1);
                        muls[prop] = (muls.ContainsKey(prop) ? muls[prop] : 1.0) * mv;
                    }
                    else if (op == "÷")
                    {
                        double dv = DataHelper.Dbl(valObj, 1);
                        if (Math.Abs(dv) > 1e-9)
                            muls[prop] = (muls.ContainsKey(prop) ? muls[prop] : 1.0) / dv;
                    }
                    else if (op == "=")
                    {
                        sets[prop] = valObj;
                    }
                    else if (op == "+")
                    {
                        double av = DataHelper.Dbl(valObj, 0);
                        adds[prop] = (adds.ContainsKey(prop) ? adds[prop] : 0.0) + av;
                    }
                }
            }

            Func<string, double, double> getVal = delegate(string key, double defVal)
            {
                double v;
                if (sets.ContainsKey(key)) v = DataHelper.Dbl(sets[key], defVal);
                else if (baseT != null && baseT.ContainsKey(key)) v = DataHelper.Dbl(baseT[key], defVal);
                else v = defVal;

                if (adds.ContainsKey(key)) v += adds[key];
                if (muls.ContainsKey(key)) v *= muls[key];
                return v;
            };

            Func<string, int> getTier = delegate(string key)
            {
                return tiers.ContainsKey(key) ? tiers[key] : 0;
            };

            // Recoil Zoomed
            string recAmtExpKey = "Recoil.Zoomed.RecoilAmountMultiplierExponent";
            string recVarExpKey = "Recoil.Zoomed.RecoilDirectionVariationMultiplierExponent";
            double recBase = getVal("Recoil.Zoomed.RecoilAmount", 0);
            double recMul = getVal("Recoil.Zoomed.RecoilAmountMultiplier", 1);
            double recExp = getVal(recAmtExpKey, 0);
            int recAmtTier = getTier(recAmtExpKey);
            double recAmount = recBase * Math.Pow(recMul, recExp + recAmtTier);

            double varBase = getVal("Recoil.Zoomed.RecoilDirectionVariation", 0);
            double varMul = getVal("Recoil.Zoomed.RecoilDirectionVariationMultiplier", 1);
            double varExp = getVal(recVarExpKey, 0);
            int recVarTier = getTier(recVarExpKey);
            double recVar = varBase * Math.Pow(varMul, varExp + recVarTier);
            double recDur = getVal("Recoil.Zoomed.RecoilDuration", 0.025);
            double recFac = getVal("Recoil.Zoomed.RecoilDecreaseFactor", 0);

            double zRad = recVar * Math.PI / 180.0;
            double sinc = zRad > 0 ? Math.Sin(zRad) / zRad : 1.0;
            double control = 96.0 / Math.Pow(1.1 * recAmount * sinc + 0.75, 2.25) + 4.0;

            // Hipfire
            double? hipfire = null;
            string clsId = DataHelper.Str(wSummary, "clsId", "");
            if (baseT != null && baseT.ContainsKey("UnzoomedMinAnglesArrayIndex") && _tblArrays.ContainsKey("hipfire"))
            {
                int cBase = DataHelper.IntKey(baseT, "UnzoomedMinAnglesArrayIndex", 0);
                bool isShotgun = clsId == "shotgun";
                int pIdx = ClampIdx(cBase + getTier("UnzoomedMinAnglesArrayIndex") - (isShotgun ? 0 : ammoHipDelta), _tblArrays["hipfire"].Length);
                double pelletDisp = isShotgun ? DataHelper.DblKey(wRaw, "pellet_disp", 0) : 0;
                double lAngle = _tblArrays["hipfire"][pIdx] + pelletDisp;
                double dScore = Math.Min(100.0, 10.0 * Math.Sqrt(0.425 / Math.Tan(1.25 * lAngle * Math.PI / 180.0) * 83.33333587646484 / 40.92409896850586));
                string hipIncKey = "DispersionBehavior.Unzoomed.Stationary.IncreasePerShot";
                double baseHipInc = DataHelper.DblKey(baseT, hipIncKey, 0);
                double curHipInc = getVal(hipIncKey, 0);
                double eMul = Math.Abs(curHipInc - baseHipInc) > 1e-9 ? Math.Sqrt(1.2) : 1.0;
                hipfire = Math.Min(100.0, dScore * eMul);
            }

            // Indices & Mobility
            string depName = DataHelper.Str(wRaw, "deploy", "dta_weapons");
            int depLen = _dtaLengths.ContainsKey(depName) ? _dtaLengths[depName] : 12;

            Func<string, int, int?> calcIdx = delegate(string k, int arrLen)
            {
                if (baseT == null || !baseT.ContainsKey(k) || baseT[k] == null) return null;
                return ClampIdx(DataHelper.IntKey(baseT, k, 0) + getTier(k), arrLen);
            };

            int? idxZda = calcIdx("MovingZoomedMinAnglesArrayIndex", _tblArrays["zda"].Length);
            int? idxZt = calcIdx("WeaponZoomTransitionIndex", _tblArrays["ida"].Length);
            int? idxAnim = (baseT != null && baseT.ContainsKey("AnimationZoomSettingsIndex"))
                ? (int?)ClampIdx(DataHelper.IntKey(baseT, "AnimationZoomSettingsIndex", 0) + getTier("WeaponZoomTransitionIndex"), _tblArrays["ida"].Length)
                : null;
            int? idxMs = calcIdx("WeaponZoomedMoveSpeedMultiplierIndex", _tblArrays["msa"].Length);
            int? idxSpr = calcIdx("SprintSettingsIndex", _tblArrays["ssa"].Length);
            int? idxDep = calcIdx("WeaponDeployTimeIndex", depLen);

            int fireSprint = getVal("CanFireWhileSprinting", 0) != 0 ? 1 : 0;
            double? mobility = null;
            if (idxDep.HasValue && idxAnim.HasValue && idxSpr.HasValue && idxMs.HasValue && idxZda.HasValue)
            {
                mobility = idxDep.Value + 4 * idxAnim.Value + idxSpr.Value + 2 * idxMs.Value + 4 * idxZda.Value + 4 * fireSprint;
            }

            double adsTime = idxZt.HasValue ? (_tblArrays["ida"][idxZt.Value] + 1.0 / 60.0) : 0.25;

            // Fire mode & RPM
            string fire = DataHelper.Str(wSummary, "fire", "全自动");
            double cycleMs = DataHelper.DblKey(wRaw, "cycle_ms", 100);
            if (fireOverride != null)
            {
                fire = fireOverride;
                double rof = DataHelper.DblKey(wRaw, "rof", 600);
                if (rof > 0) cycleMs = 60000.0 / rof;
            }
            double rpm = cycleMs > 0 ? 60000.0 / cycleMs : 600;

            // Ammo & Damage
            Dictionary<string, object> ammoMap = DataHelper.AsDict(wRaw["ammo"]);
            Dictionary<string, object> chosenAmmo = null;
            if (ammoMap != null)
            {
                foreach (string aid in equippedIds)
                {
                    if (aid != null && ammoMap.ContainsKey(aid))
                    {
                        Dictionary<string, object> cand = DataHelper.AsDict(ammoMap[aid]);
                        if (cand != null && DataHelper.BoolKey(cand, "swap")) { chosenAmmo = cand; break; }
                    }
                }
                if (chosenAmmo == null)
                {
                    foreach (string aid in equippedIds)
                    {
                        if (aid != null && ammoMap.ContainsKey(aid))
                        {
                            chosenAmmo = DataHelper.AsDict(ammoMap[aid]);
                            break;
                        }
                    }
                }
            }

            List<double[]> dmgCurve = (chosenAmmo != null && chosenAmmo.ContainsKey("dmg") && chosenAmmo["dmg"] != null)
                ? DataHelper.DmgPairs(chosenAmmo["dmg"])
                : DataHelper.DmgPairs(wRaw["dmg"]);

            Dictionary<string, object> rawMult = (chosenAmmo != null && chosenAmmo.ContainsKey("mult") && chosenAmmo["mult"] != null)
                ? DataHelper.AsDict(chosenAmmo["mult"])
                : DataHelper.AsDict(wRaw["mult"]);
            Dictionary<string, double> mult = new Dictionary<string, double>();
            if (rawMult != null)
            {
                foreach (KeyValuePair<string, object> kv in rawMult)
                    mult[kv.Key] = DataHelper.Dbl(kv.Value, 1.0);
            }
            int hsIdx = ClampIdx((int)Math.Round(getVal("Shot.DamageProtectionMultiplierIndex", 0)) + getTier("Shot.DamageProtectionMultiplierIndex"), _tblArrays["hs"].Length);
            mult["head"] = _tblArrays["hs"][hsIdx];

            int pellets = (chosenAmmo != null && chosenAmmo.ContainsKey("pellets") && chosenAmmo["pellets"] != null)
                ? DataHelper.IntKey(chosenAmmo, "pellets", 1)
                : DataHelper.IntKey(wRaw, "pellets", 1);

            // Precision lookup / interpolation
            double adsInc = getVal("DispersionBehavior.Zoomed.Stationary.IncreasePerShot", 0);
            int totalAmtExp = (baseT != null ? DataHelper.IntKey(baseT, recAmtExpKey, 0) : 0) + recAmtTier;
            int totalVarExp = (baseT != null ? DataHelper.IntKey(baseT, recVarExpKey, 0) : 0) + recVarTier;
            double? prec = EstimatePrecision(wRaw, adsInc, recDur, recFac, totalAmtExp, totalVarExp);

            ComputedStats cs = new ComputedStats();
            cs.Control = control;
            cs.Precision = prec;
            cs.Hipfire = hipfire;
            cs.Mobility = mobility;
            cs.AdsTime = adsTime;
            cs.V0 = getVal("Shot.InitialSpeed.z", 500);
            cs.Mag = (int)Math.Round(getVal("Ammo.MagazineCapacity", 30));
            cs.ReloadTac = getVal("ReloadInfoArray[0].ReloadTimeBulletsLeft", 2.5);
            cs.Rpm = rpm;
            cs.CycleMs = cycleMs;
            cs.FireMode = fire;
            cs.Sway = getVal("sway", 1.0);
            cs.FireSprint = fireSprint;
            cs.DmgCurve = dmgCurve;
            cs.Mult = mult;
            cs.Pellets = Math.Max(1, pellets);
            cs.TotalCost = totalCost;
            return cs;
        }

        private static double? EstimatePrecision(
            Dictionary<string, object> wRaw,
            double ips,
            double dur,
            double fac,
            int amtExp,
            int varExp)
        {
            IList rowsRaw = DataHelper.AsList(wRaw["prec"]);
            if (rowsRaw == null || rowsRaw.Count == 0) return null;

            List<double[]> rows = new List<double[]>();
            foreach (object rObj in rowsRaw)
            {
                double[] r = DataHelper.DoubleArray(rObj);
                if (r.Length >= 8 && Math.Abs(r[7]) < 1e-6)
                    rows.Add(r);
            }
            if (rows.Count == 0) return null;
            if (rows.Count == 1) return rows[0][1];

            double bestIps = rows[0][2];
            foreach (double[] r in rows)
                if (Math.Abs(r[2] - ips) < Math.Abs(bestIps - ips)) bestIps = r[2];

            List<double[]> rr = new List<double[]>();
            foreach (double[] r in rows)
                if (Math.Abs(r[2] - bestIps) < 1e-6) rr.Add(r);

            double bestDur = rr[0][4], bestFac = rr[0][5];
            double bestDist = double.MaxValue;
            foreach (double[] r in rr)
            {
                double d = Math.Abs(r[4] - dur) * 1000.0 + Math.Abs(r[5] - fac);
                if (d < bestDist) { bestDist = d; bestDur = r[4]; bestFac = r[5]; }
            }

            List<double[]> rr2 = new List<double[]>();
            foreach (double[] r in rr)
                if (Math.Abs(r[4] - bestDur) < 1e-6 && Math.Abs(r[5] - bestFac) < 1e-6) rr2.Add(r);

            int bestVt = (int)Math.Round(rr2[0][6]);
            foreach (double[] r in rr2)
            {
                int vt = (int)Math.Round(r[6]);
                if (Math.Abs(vt - varExp) < Math.Abs(bestVt - varExp)) bestVt = vt;
            }

            List<double[]> rr3 = new List<double[]>();
            foreach (double[] r in rr2)
                if ((int)Math.Round(r[6]) == bestVt) rr3.Add(r);

            rr3.Sort(delegate(double[] a, double[] b) { return a[3].CompareTo(b[3]); });
            double[] lo = rr3[0];
            double[] hi = rr3[rr3.Count - 1];
            if (amtExp <= lo[3])
            {
                double slope = rr3.Count > 1 ? (rr3[1][1] - lo[1]) : 0;
                return Math.Max(0, Math.Min(100, lo[1] + (amtExp - lo[3]) * slope));
            }
            if (amtExp >= hi[3])
            {
                double slope = rr3.Count > 1 ? (hi[1] - rr3[rr3.Count - 2][1]) : 0;
                return Math.Max(0, Math.Min(100, hi[1] + (amtExp - hi[3]) * slope));
            }
            for (int i = 0; i < rr3.Count - 1; i++)
            {
                if (amtExp >= rr3[i][3] && amtExp <= rr3[i + 1][3])
                {
                    double span = Math.Max(1e-9, rr3[i + 1][3] - rr3[i][3]);
                    return rr3[i][1] + (rr3[i + 1][1] - rr3[i][1]) * (amtExp - rr3[i][3]) / span;
                }
            }
            return rr3[0][1];
        }

        public static void BtkAndTtk(ComputedStats cs, double dist, out int btk, out double ttkMs)
        {
            double ut = (cs.Mult != null && cs.Mult.ContainsKey("upper_torso")) ? cs.Mult["upper_torso"] : 1.0;
            double dm = InterpDmg(cs.DmgCurve, dist) * cs.Pellets * ut;
            if (dm <= 0) { btk = 99; ttkMs = 99999; return; }
            btk = (int)Math.Ceiling(100.0 / dm - EPS);
            ttkMs = (btk - 1) * cs.CycleMs;
        }

        public static double RealTtkWithTravel(ComputedStats cs, double dist)
        {
            int btk;
            double ttk;
            BtkAndTtk(cs, Math.Max(0.5, dist), out btk, out ttk);
            return ttk + 1000.0 * dist / Math.Max(1.0, cs.V0);
        }

        public static List<DmgSegment> BuildBreakpoints(ComputedStats cs)
        {
            List<DmgSegment> res = new List<DmgSegment>();
            int prevBtk = -1;
            int curStart = 0;
            int prevTtk = 0;

            for (int d10 = 0; d10 <= 2000; d10 += 5)
            {
                double d = d10 / 10.0;
                int k;
                double t;
                BtkAndTtk(cs, Math.Max(0.05, d), out k, out t);
                int tRound = (int)Math.Round(t);
                if (prevBtk == -1)
                {
                    prevBtk = k;
                    prevTtk = tRound;
                    curStart = 0;
                }
                else if (k != prevBtk)
                {
                    int endM = (int)Math.Round(d);
                    DmgSegment seg = new DmgSegment();
                    seg.From = curStart;
                    seg.To = endM;
                    seg.Btk = prevBtk;
                    seg.TtkMs = prevTtk;
                    res.Add(seg);
                    curStart = endM;
                    prevBtk = k;
                    prevTtk = tRound;
                }
            }
            DmgSegment last = new DmgSegment();
            last.From = curStart;
            last.To = null;
            last.Btk = prevBtk;
            last.TtkMs = prevTtk;
            res.Add(last);
            return res;
        }
    }
}
