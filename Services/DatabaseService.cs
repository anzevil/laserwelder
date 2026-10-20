using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using LaserWelderApp.Models;

namespace LaserWelderApp.Services;

/// <summary>
/// SQLite 데이터베이스 서비스 (프리셋 및 작업 실적 이력 관리)
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public DatabaseService()
    {
        DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaserWelder.db");
        _connectionString = $"Data Source={DatabasePath}";
    }

    /// <summary>
    /// 데이터베이스 및 테이블 초기화 (최초 실행 시 기본 프리셋 및 샘플 실적 시드 생성)
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS WeldingPresets (
                PresetId INTEGER PRIMARY KEY,
                PresetName TEXT NOT NULL,
                ScanSpeed REAL NOT NULL,
                ScanWidth REAL NOT NULL,
                MaxPower REAL NOT NULL,
                DutyCycle REAL NOT NULL,
                Frequency REAL NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS WeldingLogs (
                LogId INTEGER PRIMARY KEY AUTOINCREMENT,
                PresetId INTEGER,
                ActionType TEXT NOT NULL,
                Details TEXT,
                Timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS WorkRecords (
                RecordId INTEGER PRIMARY KEY AUTOINCREMENT,
                WorkDate TEXT NOT NULL,
                WorkTime TEXT NOT NULL,
                PresetId INTEGER,
                ScanSpeed REAL,
                MaxPower REAL,
                WorkDuration INTEGER NOT NULL,
                TotalQuantity INTEGER NOT NULL,
                DefectQuantity INTEGER DEFAULT 0,
                GasFlow REAL,
                MeasuredBeadWidth REAL,
                OperatorName TEXT
            );
        ";
        await tableCommand.ExecuteNonQueryAsync();

        // 1. 프리셋 기본 데이터 점검 및 생성
        var checkPresetCmd = connection.CreateCommand();
        checkPresetCmd.CommandText = "SELECT COUNT(*) FROM WeldingPresets";
        var presetCount = Convert.ToInt64(await checkPresetCmd.ExecuteScalarAsync());

        if (presetCount == 0)
        {
            var seedData = new (int id, string name, double speed, double width, double power, double duty, double freq)[]
            {
                (1, "기능 설정", 150.0, 3.00, 800.0, 100.0, 2500.0),
                (2, "기능 설정", 180.0, 3.50, 900.0, 100.0, 2800.0),
                (3, "기능 설정", 200.0, 4.00, 1000.0, 90.0, 3000.0),
                (4, "기능 설정", 220.0, 4.50, 1100.0, 95.0, 3200.0),
                (5, "기능 설정", 250.0, 5.00, 1200.0, 100.0, 3500.0),
                (6, "기능 설정", 160.0, 2.50, 750.0, 85.0, 2000.0),
                (7, "기능 설정", 190.0, 3.80, 950.0, 95.0, 2900.0),
                (8, "0-8mm", 200.0, 4.00, 1000.0, 100.0, 3000.0),
                (9, "기능 설정", 300.0, 6.00, 1500.0, 100.0, 4000.0)
            };

            using var transaction = connection.BeginTransaction();
            foreach (var item in seedData)
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO WeldingPresets (PresetId, PresetName, ScanSpeed, ScanWidth, MaxPower, DutyCycle, Frequency, UpdatedAt)
                    VALUES ($id, $name, $speed, $width, $power, $duty, $freq, $updatedAt);
                ";
                insertCmd.Parameters.AddWithValue("$id", item.id);
                insertCmd.Parameters.AddWithValue("$name", item.name);
                insertCmd.Parameters.AddWithValue("$speed", item.speed);
                insertCmd.Parameters.AddWithValue("$width", item.width);
                insertCmd.Parameters.AddWithValue("$power", item.power);
                insertCmd.Parameters.AddWithValue("$duty", item.duty);
                insertCmd.Parameters.AddWithValue("$freq", item.freq);
                insertCmd.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                await insertCmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        // 2. 작업 실적 샘플 데이터 점검 및 생성 (차트 초기 표시용)
        var checkWorkCmd = connection.CreateCommand();
        checkWorkCmd.CommandText = "SELECT COUNT(*) FROM WorkRecords";
        var workCount = Convert.ToInt64(await checkWorkCmd.ExecuteScalarAsync());

        if (workCount == 0)
        {
            var baseDate = DateTime.Today.AddDays(-6);
            var sampleWorks = new (int dayOffset, string time, int preset, double speed, double power, int duration, int total, int defect, double gas, double bead, string op)[]
            {
                (0, "09:30:00", 8, 200.0, 1000.0, 45, 90, 1, 15.0, 4.02, "작업자A"),
                (0, "14:20:00", 8, 200.0, 1000.0, 50, 110, 2, 15.0, 4.06, "작업자A"),
                (1, "10:10:00", 3, 190.0, 950.0, 60, 120, 3, 14.5, 3.98, "작업자B"),
                (2, "09:00:00", 8, 200.0, 1000.0, 40, 85, 0, 15.0, 4.01, "작업자A"),
                (2, "15:00:00", 4, 220.0, 1100.0, 55, 130, 2, 15.5, 4.12, "작업자B"),
                (3, "11:00:00", 8, 200.0, 1000.0, 50, 105, 1, 15.0, 4.04, "작업자A"),
                (4, "10:30:00", 5, 250.0, 1200.0, 70, 150, 4, 16.0, 4.18, "작업자C"),
                (4, "16:00:00", 8, 200.0, 1000.0, 45, 95, 1, 15.0, 4.03, "작업자A"),
                (5, "09:40:00", 8, 200.0, 1000.0, 50, 115, 2, 15.0, 4.07, "작업자B"),
                (6, "10:00:00", 8, 200.0, 1000.0, 45, 100, 1, 15.0, 4.05, "작업자A")
            };

            using var transaction = connection.BeginTransaction();
            foreach (var item in sampleWorks)
            {
                var workDate = baseDate.AddDays(item.dayOffset).ToString("yyyy-MM-dd");
                var insertWorkCmd = connection.CreateCommand();
                insertWorkCmd.Transaction = transaction;
                insertWorkCmd.CommandText = @"
                    INSERT INTO WorkRecords (WorkDate, WorkTime, PresetId, ScanSpeed, MaxPower, WorkDuration, TotalQuantity, DefectQuantity, GasFlow, MeasuredBeadWidth, OperatorName)
                    VALUES ($wDate, $wTime, $pId, $speed, $power, $duration, $total, $defect, $gas, $bead, $op);
                ";
                insertWorkCmd.Parameters.AddWithValue("$wDate", workDate);
                insertWorkCmd.Parameters.AddWithValue("$wTime", item.time);
                insertWorkCmd.Parameters.AddWithValue("$pId", item.preset);
                insertWorkCmd.Parameters.AddWithValue("$speed", item.speed);
                insertWorkCmd.Parameters.AddWithValue("$power", item.power);
                insertWorkCmd.Parameters.AddWithValue("$duration", item.duration);
                insertWorkCmd.Parameters.AddWithValue("$total", item.total);
                insertWorkCmd.Parameters.AddWithValue("$defect", item.defect);
                insertWorkCmd.Parameters.AddWithValue("$gas", item.gas);
                insertWorkCmd.Parameters.AddWithValue("$bead", item.bead);
                insertWorkCmd.Parameters.AddWithValue("$op", item.op);
                await insertWorkCmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
    }

    /// <summary>
    /// 모든 프리셋 목록 조회
    /// </summary>
    public async Task<List<WeldingPreset>> GetAllPresetsAsync()
    {
        var list = new List<WeldingPreset>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT PresetId, PresetName, ScanSpeed, ScanWidth, MaxPower, DutyCycle, Frequency, UpdatedAt FROM WeldingPresets ORDER BY PresetId ASC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new WeldingPreset
            {
                PresetId = reader.GetInt32(0),
                PresetName = reader.GetString(1),
                ScanSpeed = reader.GetDouble(2),
                ScanWidth = reader.GetDouble(3),
                MaxPower = reader.GetDouble(4),
                DutyCycle = reader.GetDouble(5),
                Frequency = reader.GetDouble(6),
                UpdatedAt = DateTime.TryParse(reader.GetString(7), out var dt) ? dt : DateTime.Now
            });
        }
        return list;
    }

    public async Task<WeldingPreset?> GetPresetAsync(int presetId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT PresetId, PresetName, ScanSpeed, ScanWidth, MaxPower, DutyCycle, Frequency, UpdatedAt FROM WeldingPresets WHERE PresetId = $id";
        cmd.Parameters.AddWithValue("$id", presetId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new WeldingPreset
            {
                PresetId = reader.GetInt32(0),
                PresetName = reader.GetString(1),
                ScanSpeed = reader.GetDouble(2),
                ScanWidth = reader.GetDouble(3),
                MaxPower = reader.GetDouble(4),
                DutyCycle = reader.GetDouble(5),
                Frequency = reader.GetDouble(6),
                UpdatedAt = DateTime.TryParse(reader.GetString(7), out var dt) ? dt : DateTime.Now
            };
        }
        return null;
    }

    public async Task SavePresetAsync(WeldingPreset preset)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WeldingPresets (PresetId, PresetName, ScanSpeed, ScanWidth, MaxPower, DutyCycle, Frequency, UpdatedAt)
            VALUES ($id, $name, $speed, $width, $power, $duty, $freq, $updatedAt)
            ON CONFLICT(PresetId) DO UPDATE SET
                PresetName = excluded.PresetName,
                ScanSpeed = excluded.ScanSpeed,
                ScanWidth = excluded.ScanWidth,
                MaxPower = excluded.MaxPower,
                DutyCycle = excluded.DutyCycle,
                Frequency = excluded.Frequency,
                UpdatedAt = excluded.UpdatedAt;
        ";
        cmd.Parameters.AddWithValue("$id", preset.PresetId);
        cmd.Parameters.AddWithValue("$name", preset.PresetName);
        cmd.Parameters.AddWithValue("$speed", preset.ScanSpeed);
        cmd.Parameters.AddWithValue("$width", preset.ScanWidth);
        cmd.Parameters.AddWithValue("$power", preset.MaxPower);
        cmd.Parameters.AddWithValue("$duty", preset.DutyCycle);
        cmd.Parameters.AddWithValue("$freq", preset.Frequency);
        cmd.Parameters.AddWithValue("$updatedAt", now);

        await cmd.ExecuteNonQueryAsync();
        await LogActionInternalAsync(connection, preset.PresetId, "SAVE", 
            $"속도:{preset.ScanSpeed}, 폭:{preset.ScanWidth}, 출력:{preset.MaxPower}, 사용률:{preset.DutyCycle}, 주파수:{preset.Frequency}");
    }

    public async Task LogActionAsync(int presetId, string actionType, string details)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await LogActionInternalAsync(connection, presetId, actionType, details);
    }

    private static async Task LogActionInternalAsync(SqliteConnection connection, int presetId, string actionType, string details)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WeldingLogs (PresetId, ActionType, Details, Timestamp)
            VALUES ($presetId, $actionType, $details, $timestamp);
        ";
        cmd.Parameters.AddWithValue("$presetId", presetId);
        cmd.Parameters.AddWithValue("$actionType", actionType);
        cmd.Parameters.AddWithValue("$details", details);
        cmd.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        await cmd.ExecuteNonQueryAsync();
    }

    // ================= WorkRecords (작업 실적) 관련 =================

    /// <summary>
    /// 모든 작업 실적 이력 조회 (최신순 정렬)
    /// </summary>
    public async Task<List<WorkRecord>> GetAllWorkRecordsAsync()
    {
        var list = new List<WorkRecord>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT RecordId, WorkDate, WorkTime, PresetId, ScanSpeed, MaxPower, WorkDuration, TotalQuantity, DefectQuantity, GasFlow, MeasuredBeadWidth, OperatorName
            FROM WorkRecords
            ORDER BY WorkDate ASC, WorkTime ASC;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new WorkRecord
            {
                RecordId = reader.GetInt32(0),
                WorkDate = reader.GetString(1),
                WorkTime = reader.GetString(2),
                PresetId = reader.IsDBNull(3) ? 8 : reader.GetInt32(3),
                ScanSpeed = reader.IsDBNull(4) ? 200.0 : reader.GetDouble(4),
                MaxPower = reader.IsDBNull(5) ? 1000.0 : reader.GetDouble(5),
                WorkDuration = reader.GetInt32(6),
                TotalQuantity = reader.GetInt32(7),
                DefectQuantity = reader.GetInt32(8),
                GasFlow = reader.IsDBNull(9) ? 15.0 : reader.GetDouble(9),
                MeasuredBeadWidth = reader.IsDBNull(10) ? 4.0 : reader.GetDouble(10),
                OperatorName = reader.IsDBNull(11) ? "작업자" : reader.GetString(11)
            });
        }
        return list;
    }

    /// <summary>
    /// 신규 작업 실적 등록
    /// </summary>
    public async Task AddWorkRecordAsync(WorkRecord record)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WorkRecords (WorkDate, WorkTime, PresetId, ScanSpeed, MaxPower, WorkDuration, TotalQuantity, DefectQuantity, GasFlow, MeasuredBeadWidth, OperatorName)
            VALUES ($wDate, $wTime, $pId, $speed, $power, $duration, $total, $defect, $gas, $bead, $op);
        ";
        cmd.Parameters.AddWithValue("$wDate", record.WorkDate);
        cmd.Parameters.AddWithValue("$wTime", record.WorkTime);
        cmd.Parameters.AddWithValue("$pId", record.PresetId);
        cmd.Parameters.AddWithValue("$speed", record.ScanSpeed);
        cmd.Parameters.AddWithValue("$power", record.MaxPower);
        cmd.Parameters.AddWithValue("$duration", record.WorkDuration);
        cmd.Parameters.AddWithValue("$total", record.TotalQuantity);
        cmd.Parameters.AddWithValue("$defect", record.DefectQuantity);
        cmd.Parameters.AddWithValue("$gas", record.GasFlow);
        cmd.Parameters.AddWithValue("$bead", record.MeasuredBeadWidth);
        cmd.Parameters.AddWithValue("$op", record.OperatorName);

        await cmd.ExecuteNonQueryAsync();
    }
}
