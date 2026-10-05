# -*- coding: utf-8 -*-
"""ScalingStatGen - 从 SkillDepot 描述符推导角色缩放属性。

四层管道（顺序固定）：
  L0 语境排除  -> 护盾/治疗/面板增益/防御无视/生命消耗（展开为 6 类）
  L1 纯倍率    -> 描述符「伤害|{param}属性」或 prose「伤害...基于X」
  L2 加算      -> 「属性 的 N%」进入基础乘区
  L3 转换      -> 「攻击力提高|{param}属性」/「基于X，提高攻击力」
输出 base_stat + scaling_source（两字段分离，因 L3 两者不同）。

用法: python generate.py [--emit]
"""
import json, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
META = os.path.join(ROOT, "src", "Ellen.Launcher", "Ellen.Launcher", "Resource", "Metadata", "Genshin", "CHS")
OUT  = os.path.join(ROOT, "src", "Ellen.Launcher", "Ellen.Launcher", "Resource", "Derived", "AvatarScalingStat.json")

STATS = ("生命值上限", "攻击力", "防御力")
SHORT = {"生命值上限": "HP", "攻击力": "ATK", "防御力": "DEF"}
S_RE = "|".join(STATS)

# ---------------- L0：语境排除（span 掩码，不是丢弃整句） ----------------
# 掩码而非丢弃：诺艾尔「造成岩元素伤害，并形成护盾，伤害吸收量基于...防御力」是复合句。
L0_SPAN = [
    ("shield",  re.compile(r"伤害吸收量[^。；\n]{0,10}?(?:基于|受益于|相当于|等同于)[^。；，\n]{0,12}")),
    ("shield",  re.compile(r"护盾[^。；\n]{0,14}?(?:基于|受益于|相当于|等同于)[^。；，\n]{0,12}")),
    ("heal",    re.compile(r"(?:恢复|回复)生命值[^。；，\n]{0,14}")),
    ("heal",    re.compile(r"回复量[^。；，\n]{0,16}")),
    ("heal",    re.compile(r"治疗量[^。；，\n]{0,16}")),
    ("cost",    re.compile(r"消耗生命值[^。；，\n]{0,16}")),
    ("shred",   re.compile(r"无视[^。；，\n]{0,8}?\d+(?:\.\d+)?%的防御力")),
    ("shred",   re.compile(r"防御力(?:降低|下降|减少)[^。；，\n]{0,8}")),
    ("shred",   re.compile(r"抗性(?:降低|下降|减少)[^。；，\n]{0,8}")),
    ("consumable", re.compile(r"不能超过[^。；，\n]{0,20}")),
]
# 面板增益：属性±6字内出现 提高/提升/增加 + 百分比，且句内无「造成的伤害」/「伤害提高」
PANEL = re.compile(r"(?:攻击力|防御力|生命值上限)[^。；·\n]{0,6}?(?:提高|提升|增加)[^。；·，\n]{0,4}?\d+(?:\.\d+)?%")
DMG_HINT = re.compile(r"造成的伤害|伤害提高|伤害提升|伤害值|技能伤害")

def mask_l0(sent):
    reason = None
    def sub(m):
        nonlocal reason
        reason = reason or m[0]
        return " " * len(m[0])
    out = sent
    for kind, pat in L0_SPAN:
        out2, n = pat.subn(lambda m: (" " * len(m.group(0))), out)
        if n and reason is None:
            reason = kind
        out = out2
    if PANEL.search(out) and not DMG_HINT.search(out):
        out = PANEL.sub(lambda m: " " * len(m.group(0)), out)
        reason = reason or "panel"
    return out, reason

# ---------------- 描述符解析 ----------------
DESC = re.compile(r"^(?P<label>[^|]{1,40})\|(?P<spec>.+)$")
L0_LABEL = re.compile(r"护盾|吸收量|治疗|回复|恢复|消耗生命值|抗性")
DMG_LABEL = re.compile(r"伤害")
BOOST_LABEL = re.compile(r"攻击力(?:提高|提升|增加|加成)")
def stats_in(text):
    return {SHORT[m.group(0)] for m in re.finditer(S_RE, text)}

# ---------------- prose 规则（掩码后） ----------------
P_DMG_BASED = re.compile(r"(?:基于|相当于|等同于)[^。；，\n]{0,16}?(生命值上限|攻击力|防御力)")
P_DMG_PCT   = re.compile(r"(生命值上限|攻击力|防御力)的[^。；，\n]{0,4}?\d+(?:\.\d+)?%")
P_CONV      = re.compile(r"基于[^。；，\n]{0,16}?(生命值上限|攻击力|防御力)[^。；，\n]{0,10}?(?:提高|提升|增加)[^。；，\n]{0,6}?攻击力")
P_DUAL      = re.compile(r"攻击力和防御力|防御力和攻击力")

def segments(a):
    sd = a.get("SkillDepot") or {};
    out = []
    def add(txt, origin):
        if isinstance(txt, str) and txt.strip():
            out.append((origin, txt))
    def emit(sk, origin):
        if not isinstance(sk, dict):
            return
        add(sk.get("Description"), origin)
        for d in ((sk.get("Proud") or {}).get("Descriptions") or []):
            add(d, origin)
    for sk in (sd.get("Skills") or []):
        emit(sk, "skill")
    emit(sd.get("EnergySkill"), "burst")
    for sk in (sd.get("Inherents") or []):
        emit(sk, "inherent")
    for t in (sd.get("Talents") or []):
        emit(t, "constellation")
    return out

def derive(a):
    dmg, conv, cond_dmg, cond_conv, l0 = set(), set(), set(), set(), set()
    # L0 排除掉的语境本身携带缩放属性，而它正是权重兜底最需要的输入：
    # 塔利雅 HpPercent=100 就是因为护盾吃 HP。丢弃该属性 = 复现塔利雅误判。
    l0_scaling = {}
    saw_dmg_descriptor = False
    for origin, raw in segments(a):
        for sent in re.split(r"[。\n]", raw):
            if len(sent.strip()) < 4:
                continue
            is_cond = origin == "constellation"
            # 1) 描述符优先（机器可读）
            m = DESC.match(sent.strip())
            if m:
                label, spec = m.group("label"), m.group("spec")
                if L0_LABEL.search(label):
                    sc = stats_in(spec)
                    if sc:
                        key = "shield" if re.search(r"护盾|吸收量", label) else (
                              "heal" if re.search(r"治疗|回复|恢复", label) else "other")
                        l0_scaling.setdefault(key, set()).update(sc)
                    l0.add("label:" + label.strip()[:16]); continue
                if not DMG_LABEL.search(label) and not BOOST_LABEL.search(label):
                    continue
                sc = stats_in(spec)
                if not sc:
                    # 伤害描述符但无缩放属性后缀 —— 例「一段伤害|{param1:F1P}」。
                    # 这本身就是规则：无后缀 ⇒ 吃 ATK。不是「没信号」。
                    if DMG_LABEL.search(label):
                        saw_dmg_descriptor = True
                    continue
                if DMG_LABEL.search(label):
                    saw_dmg_descriptor = True
                    (cond_dmg if is_cond else dmg).update(sc); continue
                if BOOST_LABEL.search(label):
                    (cond_conv if is_cond else conv).update(sc); continue
                continue
            # 2) prose（先掩码 L0）
            masked, why = mask_l0(sent)
            if why:
                l0.add(why)
            if not masked.strip():
                continue
            sc = set()
            dual = bool(P_DUAL.search(masked))
            if DMG_HINT.search(masked):
                sc |= {SHORT[x] for x in P_DMG_BASED.findall(masked)}
                sc |= {SHORT[x] for x in P_DMG_PCT.findall(masked)}
            if P_CONV.search(masked):
                sc |= {SHORT[x] for x in P_CONV.findall(masked)}
                (cond_conv if is_cond else conv).update({SHORT[x] for x in P_CONV.findall(masked)})
                sc -= {SHORT[x] for x in P_CONV.findall(masked)}
            if sc:
                (cond_dmg if is_cond else dmg).update(sc)
            if dual and (dmg or conv):
                (cond_dmg if is_cond else dmg).update({"ATK", "DEF"})

    # ---- 合并 ----
    # 规则：L3 转换（「基于X，提高攻击力」）意味着伤害落在攻击区 => base_stat=ATK，来源记在 scaling_source。
    # 无转换时 L1/L2 的缩放属性即 base_stat 与 scaling_source 同值。
    variants = None
    multi = None
    if len(dmg) > 1:
        multi, base, src = sorted(dmg), None, None
    elif conv:
        base, src = "ATK", next(iter(conv))
        if dmg and dmg != conv:
            variants = sorted(dmg)
    elif dmg:
        base = src = next(iter(dmg))
    else:
        base = src = "ATK"                # default_atk 兜底
    if len(conv) > 1:
        multi, base, src = sorted(conv), None, None
    cond = sorted(cond_dmg | cond_conv) or None
    if cond and not (dmg or conv):
        cond_note = "base 全 ATK；仅命座条件性按 %s" % "/".join(cond)
    else:
        cond_note = None
    # ATK 的可信度必须三分，不能混成一个空值：
    #   explicit        描述符/prose 明确写了攻击力
    #   default_stat    存在伤害描述符但无缩放属性后缀（如「一段伤害|{param1:F1P}」）
    #                   —— 这是规则本身，可信
    #   no_damage_signal 完全没有伤害类描述符，取值仅由 default_atk 兜底
    if "ATK" in (dmg | conv | cond_dmg | cond_conv):
        atk_basis = "explicit"
    elif saw_dmg_descriptor:
        atk_basis = "default_stat"
    else:
        atk_basis = "no_damage_signal"
    is_default = atk_basis == "no_damage_signal"
    return {"base_stat": base, "scaling_source": src, "multi": multi,
            "base_stat_variants": variants,
            "conditional_scaling": cond, "conditional_note": cond_note,
            "atk_basis": atk_basis, "is_default_atk": is_default,
            "l0_scaling": {k: sorted(v) for k, v in l0_scaling.items()} or None,
            "l0_excluded": sorted(l0) or None}

# ---------------- 金标准 ----------------
def load_dir():
    d = {}
    for f in sorted(os.listdir(os.path.join(META, "Avatar"))):
        if f.endswith(".json"):
            a = json.load(open(os.path.join(META, "Avatar", f), encoding="utf-8"))
            d[a["Name"]] = a
    return d

EXT = [
    ("阿贝多",   {"base_stat": "DEF", "scaling_source": "DEF"}),
    ("钟离",     {"base_stat": "HP",  "scaling_source": "HP"}),
    ("胡桃",     {"base_stat": "ATK", "scaling_source": "HP"}),
    ("辛焱",     {"base_stat": "ATK", "scaling_source": "ATK"}),
    ("诺艾尔",   {"base_stat": "ATK", "scaling_source": "DEF"}),
    ("千织",     {"multi": ["ATK", "DEF"]}),
    ("莱欧斯利", {"base_stat": "ATK", "scaling_source": "ATK"}),
    ("娜维娅",   {"base_stat": "ATK", "scaling_source": "ATK"}),
]

def main():
    sys.stdout.reconfigure(encoding="utf-8")
    av = load_dir()
    wt = json.load(open(os.path.join(META, "ReliquaryAffixWeight.json"), encoding="utf-8"))
    prio, rows = {}, {}
    for e in wt:
        p = os.path.join(META, "Avatar", "%s.json" % e["AvatarId"])
        if not os.path.exists(p):
            continue
        a = json.load(open(p, encoding="utf-8"))
        s = "HP" if e["HpPercent"] else ("DEF" if e["DefensePercent"] else "ATK")
        prio[a["Name"]] = s
    for n, a in av.items():
        rows[n] = dict(derive(a), id=a["Id"])
        rows[n]["stat_priority"] = prio.get(n)

    print("=== 外部证据（%d 条）===" % len(EXT))
    eok = 0
    for n, exp in EXT:
        got = rows[n]
        good = all(got.get(k) == v for k, v in exp.items())
        eok += good
        print("  %-9s %-46s %-52s %s" % (n, exp,
              {k: got[k] for k in ("base_stat", "scaling_source", "multi", "conditional_scaling")},
              "OK" if good else "MISS"))
    print("\n=== 权重表隐含 %d 条（语义：stat_priority，非 scaling_source）===" % len(prio))
    wok = 0
    miss = []
    for n, p in prio.items():
        got = rows[n]
        agree = bool(got["scaling_source"] == p or (got["multi"] and p in got["multi"]))
        wok += agree
        if not agree:
            miss.append((n, p, got))
    print("   一致 %d/%d" % (wok, len(prio)))
    for n, p, got in miss:
        print("   DIVERGE %-9s 权重=%-4s 派生=%s  L0=%s" % (n, p, got["scaling_source"] or got["multi"], got["l0_excluded"]))
    print("\n合计 %d/31" % (eok + wok))
    # 分歧必须显式落盘，不能留静默错值。stat_priority 来自权重表（含护盾/治疗语境），
    # scaling_source 来自本生成器（已排除该语境），两者语义不同，分歧处人工复核。
    divergences = [n for n, p in prio.items()
                   if not (rows[n]["scaling_source"] == p or (rows[n]["multi"] and p in rows[n]["multi"]))]
    if "--emit" in sys.argv:
        out = {"sourceType": "derived-original", "generator": "tools/ScalingStatGen/generate.py",
               "inputSnapshot": "Resource/Metadata/Genshin/CHS/Avatar/*.json",
               "reviewStatus": "draft",
               "reviewNote": "尚未经第二人复核。塔利雅一类分歧（权重表按护盾语境给 HP，伤害缩放为 ATK）属语义差异而非错误。",
               "fields": ["base_stat", "scaling_source", "multi", "conditional_scaling", "stat_priority"],
               "statPriorityIsNotScalingSource": True,
               "divergences": divergences,
               "characters": rows}
        os.makedirs(os.path.dirname(OUT), exist_ok=True)
        json.dump(out, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
        print("\n已写出 %s (%d 角色)" % (OUT, len(rows)))

main()

