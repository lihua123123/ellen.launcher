# -*- coding: utf-8 -*-
"""verify-vectors.py —— 独立复算器。
只读取 golden-vectors.json 的字段，不访问任何元数据 / 不引用生成过程。
用途：第二人复核（P0-1 的 reviewStatus: verified 前置条件）。
"""
import json, os, sys

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), "golden-vectors.json")
doc = json.load(open(P, encoding="utf-8"))

def res_coef(r):                      # sections[0].blocks[1]
    if r <= 0:      return 1 - r/2
    if r <= 0.75:   return 1 - r
    return 1/(4*r + 1)
def def_coef(lc, lm, dr, di):         # sections[0].blocks[3]
    return (lc+100)/((lc+100) + (lm+100)*(1-dr)*(1-di))
def amp(em, base, bonus):             # sections[2].blocks[1]
    return base*(1 + 2.78*em/(em+1400) + bonus)
def trans(em, coef, k, bonus):        # sections[4].blocks[1]
    return k*coef*(1 + 16*em/(em+2000) + bonus)

fails, checks = [], 0
for v in doc["vectors"]:
    i, b, vid = v["case"]["inputs"], v["expected"]["breakdown"], v["id"]
    tol = v["expected"]["tolerance"]
    checks += 1
    if tol != 0: fails.append(f"{vid}: tolerance != 0")
    fam = v["family"]
    if fam == "DMG" and vid != "GV-DMG-007":
        ta = b["baseAtk"]*(1+b["atkPct"]) + b["flatAtk"]
        if ta != b["totalAtk"]: fails.append(f"{vid}: totalAtk 不一致")
        d = def_coef(90, i["enemy"]["level"], i["enemy"]["defReduction"], i["enemy"]["defIgnore"])
        if d != b["defense"]: fails.append(f"{vid}: defense 不一致 ({d} vs {b['defense']})")
        r = res_coef(i["enemy"]["resistance"])
        if r != b["resistance"]: fails.append(f"{vid}: resistance 不一致 ({r} vs {b['resistance']})")
        rx = 1.0
        if i["assumptions"]["reaction"] == "amplifying":
            rx = amp(i["assumptions"]["elementMastery"], 2.0, 0.0)
        if abs(rx - b["reaction"]) > 1e-12: fails.append(f"{vid}: reaction 不一致")
        f = b["totalAtk"]*b["multiplier"]*(1+b["dmgBonus"])*b["crit"]*b["defense"]*b["resistance"]*b["reaction"]
        if abs(f - b["final"]) > 1e-12: fails.append(f"{vid}: final 不一致")
        print(f"  {vid}  totalAtk={ta:.6f}  def={d:.6f}  res={r:.6f}  react={rx:.6f}  final={b['final']:.6f}")
    elif vid == "GV-DMG-007":
        for k in ("totalAtk","multiplier","dmgBonus","crit","defense"):
            if b.get(k) is not None: fails.append(f"{vid}: {k} 必须为 null（剧变不参与）")
        t = trans(i["assumptions"]["elementMastery"], b["reactionCoefficient"], 1446.85, 0.0)
        if abs(t - b["transBase"]) > 1e-12: fails.append(f"{vid}: transBase 不一致")
        f = t*res_coef(i["enemy"]["resistance"])
        if abs(f - b["final"]) > 1e-12: fails.append(f"{vid}: final 不一致")
        print(f"  {vid}  transBase={t:.6f}  final={b['final']:.6f}")
    elif fam == "SCR":
        s = 0.0
        for row in b["rows"]:
            if row["normalized"] is None:
                if row["score"] != 0: fails.append(f"{vid}: 不可评分词条 {row['substat']} 得分非 0")
            else:
                exp = row["normalized"]*(row["weight"]/100.0)
                if abs(exp - row["score"]) > 1e-12: fails.append(f"{vid}: {row['substat']} 得分不一致")
                s += row["score"]
        if abs(s - b["score"]) > 1e-12: fails.append(f"{vid}: score 不一致")
        if abs(b["score"]/b["maxScore"] - b["rating"]) > 1e-12: fails.append(f"{vid}: rating 不一致")
        print(f"  {vid}  score={b['score']:.6f}  rating={b['rating']:.6f}  rows={len(b['rows'])}")
    elif fam == "ER":
        if not b["monotonicIncreasing"]: fails.append(f"{vid}: 单调性为假")
        e = b["baseValue"]*2.0*b["absorb"]
        if abs(e - b["final"]) > 1e-12: fails.append(f"{vid}: final 不一致")
        print(f"  {vid}  baseValue={b['baseValue']}  absorb={b['absorb']}  final={b['final']}")

print(f"\n校验 {checks} 条：{'全部通过' if not fails else '失败'}")
for f in fails: print("  FAIL", f)
sys.exit(1 if fails else 0)
