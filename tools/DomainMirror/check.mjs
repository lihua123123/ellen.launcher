// 跨语言数值校验：与 C# Ellen.Launcher.Test/Resistance 使用同一张手算表。
//
// 两端都不是对方的 oracle —— 期望值是手算的，不是任一端跑出来的输出。
// 两端同时通过同一张手算表，即彼此一致的传递性证明。
//
// 用法: node check.mjs   (不一致时退出码 1)

import {
  ResistanceElement,
  ResistanceMath,
  ResistanceResolver,
  ResistanceSource,
  ResistanceSourceKind,
  ResistanceTiers,
} from './dist/resistance.js';

const TOLERANCE = 1e-12;

// 边界取自 Monster.json BaseValue.*SubHurt 的实测取值域（16 档），不是手挑的整数。
const HAND_COMPUTED = [
  [-0.5, 1.25],
  [-0.4, 1.2],
  [-0.2, 1.1],
  [0.0, 1.0],
  [0.1, 0.9],
  [0.15, 0.85],
  [0.2, 0.8],
  [0.25, 0.75],
  [0.3, 0.7],
  [0.4, 0.6],
  [0.5, 0.5],
  [0.6, 0.4],
  [0.7, 0.3],
  [0.75, 0.25],
  [0.9, 0.217391304347826],
  [1.0, 0.2],
  [1.35, 0.15625],
  [2.0, 0.111111111111111],
];

const REALIZED_DOMAIN = [-0.5, -0.4, -0.2, 0.0, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 0.9, 1.35, 2.0];

const failures = [];
let checks = 0;

function expect(label, actual, expected, tolerance = 0) {
  checks++;
  const delta = Math.abs(actual - expected);
  if (!(delta <= tolerance)) {
    failures.push(`${label}: 期望 ${expected}, 实得 ${actual}, Δ=${delta}`);
  }
}

for (const [resistance, expected] of HAND_COMPUTED) {
  expect(`coefficient(${resistance})`, ResistanceMath.coefficient(resistance), expected, TOLERANCE);
}

// 分段交界处的连续性。
expect('coefficient(0)', ResistanceMath.coefficient(0), 1.0, TOLERANCE);
expect('coefficient(-0)', ResistanceMath.coefficient(-0), 1.0, TOLERANCE);
expect('coefficient(0.75)', ResistanceMath.coefficient(0.75), 0.25, TOLERANCE);
expect('coefficient(0.75-ε)', ResistanceMath.coefficient(0.75 - 1e-9), ResistanceMath.coefficient(0.75 + 1e-9), 1e-8);

// 非有限输入：类型即契约，不吞错——返回 0（伤害归零是可见的失败）而非把 NaN 传播下去。
expect('coefficient(NaN)', ResistanceMath.coefficient(Number.NaN), 0, 0);
expect('coefficient(+Infinity)', ResistanceMath.coefficient(Number.POSITIVE_INFINITY), 0, 0);
expect('coefficient(-Infinity)', ResistanceMath.coefficient(Number.NEGATIVE_INFINITY), 0, 0);

// 实测域上的严格单调递减。
for (let i = 1; i < REALIZED_DOMAIN.length; i++) {
  const previous = ResistanceMath.coefficient(REALIZED_DOMAIN[i - 1]);
  const current = ResistanceMath.coefficient(REALIZED_DOMAIN[i]);
  if (!(current < previous)) {
    failures.push(`单调性: r=${REALIZED_DOMAIN[i]} 未严格小于 r=${REALIZED_DOMAIN[i - 1]}`);
  }
}

// 档位常量与元素维度。
expect('ResistanceTiers.Universal', ResistanceTiers.Universal, 0.1, 0);
expect('ResistanceTiers.Default', ResistanceTiers.Default, ResistanceTiers.Universal, 0);
expect('ResistanceElement.Physical', ResistanceElement.Physical, 0, 0);
expect('ResistanceElement.Rock', ResistanceElement.Rock, 7, 0);

// 优先级：用户 > 规则 > 记忆 > 默认。
const memory = new ResistanceSource(ResistanceSourceKind.Memory, 0.3);
const rule = new ResistanceSource(ResistanceSourceKind.Rule, 0.5);
const user = new ResistanceSource(ResistanceSourceKind.User, -0.4);
expect('resolve(空)', ResistanceResolver.resolve([]).value, ResistanceTiers.Default, 0);
expect('resolve(memory)', ResistanceResolver.resolve([memory]).value, 0.3, 0);
expect('resolve(memory,rule)', ResistanceResolver.resolve([memory, rule]).value, 0.5, 0);
expect('resolve(memory,rule,user)', ResistanceResolver.resolve([memory, rule, user]).value, -0.4, 0);
expect('resolve(含 null)', ResistanceResolver.resolve([null, rule]).value, 0.5, 0);
expect('resolve 返回来源', ResistanceResolver.resolve([memory, rule]).kind, ResistanceSourceKind.Rule, 0);

if (failures.length === 0) {
  console.log(`DomainMirror: ${checks} checks passed, 0 failed`);
  process.exit(0);
}

console.log(`DomainMirror: ${failures.length} check(s) failed`);
for (const failure of failures) {
  console.log(`  ${failure}`);
}
process.exit(1);
