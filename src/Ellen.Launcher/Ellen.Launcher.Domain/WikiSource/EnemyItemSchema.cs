// enemy-item-v1.1 契约版本常量。冻结清单见 workspace/wiki-migration/reports/enemy-item-v1.1.json。
// 字段增减、类型变更、新增 union 必须走 ADR 变更并同步更新 ContractFreezeTest。

namespace Ellen.Launcher.Domain.WikiSource;

public static class EnemyItemSchema
{
    public const string SchemaVersion = "enemy-item-v1.1";
    public const string Supersedes = "enemy-item-v1";
    public const int ExpectedItemCount = 753;

    /// <summary>shields[].duration 的 union 类型（ADR R2-1）。DTO 用 JsonElement? 接收。</summary>
    public static readonly string[] ShieldDurationTypes = ["string", "number"];
}
