import { ResistanceMath, ResistanceTiers } from '../domain/resistance';

/** Monster.json BaseValue.*SubHurt 的实测取值域（16 档）。 */
const REALIZED_TIERS: number[] = [
  -0.5, -0.4, -0.2, 0, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 0.9, 1.35, 2
];

@Entry
@Component
struct Index {
  @State rows: string[] = [];

  aboutToAppear(): void {
    const next: string[] = [];
    for (const r of REALIZED_TIERS) {
      next.push(`r = ${r}    k(r) = ${ResistanceMath.coefficient(r)}`);
    }
    this.rows = next;
  }

  build() {
    Column() {
      Text('抗性分段 k(r)').fontSize(20).fontWeight(FontWeight.Bold).margin({ top: 16 })
      Text(`实测 16 档 · 默认 universal = ${ResistanceTiers.Default}`)
        .fontSize(12)
        .opacity(0.6)
        .margin({ top: 4, bottom: 12 })
      List({ space: 6 }) {
        ForEach(this.rows, (line: string) => {
          ListItem() {
            Text(line).fontSize(14).width('100%')
          }
        }, (line: string) => line)
      }
      .layoutWeight(1)
      .width('100%')
      .padding({ left: 16, right: 16 })
      Text('理论估算 · 非官方数值').fontSize(11).opacity(0.5).margin({ bottom: 12 })
    }
    .width('100%')
    .height('100%')
  }
}