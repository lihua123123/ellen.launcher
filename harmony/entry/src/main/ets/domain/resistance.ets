/**
 * Ellen.Launcher.Domain/Resistance 的 ArkTS 镜像。
 *
 * C2：C# 为唯一真源，本文件是镜像。数值有差异回 C# 改真源后重镜像，不在 UI 修。
 * 与 tools/DomainMirror/src/resistance.ts 逐函数一致，两端共用同一张手算表校验。
 */

/** 抗性元素维度，顺序与 Monster.json 的 BaseValue 字段后缀一一对应。 */
export enum ResistanceElement {
  Physical = 0,
  Fire = 1,
  Water = 2,
  Grass = 3,
  Elec = 4,
  Wind = 5,
  Ice = 6,
  Rock = 7,
}

/**
 * 兜底档位常量。主源是随包元数据的 BaseValue.*SubHurt（474/562），
 * 本表只服务缺 BaseValue 的 88 条与「无来源」默认值。
 */
export class ResistanceTiers {
  /** 通用档，同时是「无来源」时的默认值。 */
  static readonly Universal: number = 0.10;
  /** 对自身元素高抗。实测常见 0.5。 */
  static readonly ElementalHigh: number = 0.40;
  /** 完全免疫。实测存在 1.35 / 2.0 的超免疫，仍走第三段。 */
  static readonly Immune: number = 1.00;
  /** 负抗特例。实测存在 -0.4 / -0.5。 */
  static readonly Negative: number = -0.20;
  /** 默认值站在安全侧：低估伤害而非高估。 */
  static readonly Default: number = ResistanceTiers.Universal;
}

/** 抗性分段函数。引擎只做算术，r 由调用方作为入参提供。 */
export class ResistanceMath {
  /**
   * 抗性乘区系数 k(r)：非有限输入 → 0；r < 0 → 1 - r/2；r < 0.75 → 1 - r；否则 1 / (4r + 1)。
   *
   * 两处在 r = 0 与 r = 0.75 连续（均为 1 与 0.25）。
   * 实测取值域包含 r = 1.35 与 r = 2.0，落在第三段。
   * 非有限输入返回 0 而非抛出：结果归零是可见的失败，不会伪装成看似合理的伤害数值。
   */
  static coefficient(resistance: number): number {
    if (!Number.isFinite(resistance)) {
      return 0;
    }
    if (resistance < 0) {
      return 1 - resistance / 2;
    }
    if (resistance < 0.75) {
      return 1 - resistance;
    }
    return 1 / (4 * resistance + 1);
  }
}

/** 抗性取值来源，数值越大优先级越高。 */
export enum ResistanceSourceKind {
  Default = 0,
  Memory = 1,
  Rule = 2,
  User = 3,
}

/** 一个带出处的抗性候选值。 */
export class ResistanceSource {
  readonly kind: ResistanceSourceKind;
  readonly value: number;

  constructor(kind: ResistanceSourceKind, value: number) {
    this.kind = kind;
    this.value = value;
  }

  static createDefault(): ResistanceSource {
    return new ResistanceSource(ResistanceSourceKind.Default, ResistanceTiers.Default);
  }
}

/**
 * 按优先级挑选抗性入参。这属于领域服务而非引擎：引擎只接受一个已确定的
 * resistance 入参，不查表、不硬编码任何抗性字面量。
 */
export class ResistanceResolver {
  /** 在候选中选出优先级最高者；全为 null 时回退 ResistanceTiers.Default。 */
  static resolve(candidates: (ResistanceSource | null)[]): ResistanceSource {
    let best: ResistanceSource | null = null;
    for (const candidate of candidates) {
      if (candidate === null) {
        continue;
      }
      if (best === null || candidate.kind > best.kind) {
        best = candidate;
      }
    }
    if (best === null) {
      return ResistanceSource.createDefault();
    }
    return best;
  }
}