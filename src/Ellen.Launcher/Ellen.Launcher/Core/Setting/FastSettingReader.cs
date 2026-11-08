// 启动期「只读一行设置」的快速通道。
//
// 为什么需要它：InitializeCulture 必须在任何 XAML 加载之前拿到 PrimaryLanguage，
// 而走 CultureOptions（EF 属性）会触发 EF 首次查询——实测代价：
//   EF 模型构建 296ms + 迁移枚举 382ms + 首个查询 550ms ≈ 1.2s，全在启动主路径上。
// 这里用 Microsoft.Data.Sqlite 直接读 settings 表（同一份数据、同一张表、无第二数据源），
// 只付出打开连接的成本；EF 的模型/迁移开销则推迟到后台初始化。
//
// 失败（文件缺失、表不存在、被锁）一律返回 null，由调用方回退到系统区域设置——
// 与「读不到设置就用默认值」的既有语义一致，绝不抛异常阻断启动。

using Microsoft.Data.Sqlite;
using System.IO;

namespace Ellen.Launcher.Core.Setting;

public static class FastSettingReader
{
    /// <summary>读取单个设置项；任何异常都返回 null。</summary>
    public static string? TryRead(string key)
    {
        try
        {
            string dbFile = EllenRuntime.GetDataDirectoryFile("Userdata.db");
            if (!File.Exists(dbFile))
            {
                return null;
            }

            // ReadOnly + 短超时：绝不长时间占着库，避免和后台迁移抢锁。
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = dbFile,
                Mode = SqliteOpenMode.ReadOnly,
                DefaultTimeout = 1,
            };

            using SqliteConnection connection = new(builder.ToString());
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "select \"Value\" from \"settings\" where \"Key\" = $key limit 1";
            command.Parameters.AddWithValue("$key", key);

            return command.ExecuteScalar() as string;
        }
        catch (SqliteException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
