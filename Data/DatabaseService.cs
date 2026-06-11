using System.IO;
using HomeschoolPlanner.Models;
using HomeschoolPlanner.Services;
using Microsoft.Data.Sqlite;

namespace HomeschoolPlanner.Data;

// Single entry point for all database access.
// The .db file lives next to the .exe so the app is fully self-contained.
public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeschoolPlanner");
        Directory.CreateDirectory(folder); // no-op if it already exists
        var dbPath = Path.Combine(folder, "homeschool.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeSchema();
    }

    // Creates tables if they don't exist and migrates any missing columns
    private void InitializeSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var sql = @"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS GradeClasses (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                GradeKey     TEXT NOT NULL,
                Name         TEXT NOT NULL,
                Color        TEXT NOT NULL DEFAULT '#4A7CB5',
                ScheduleType TEXT NOT NULL DEFAULT 'EveryDay',
                ScheduleDays TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Students (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Name  TEXT NOT NULL,
                Grade TEXT NOT NULL DEFAULT '',
                Color TEXT NOT NULL DEFAULT '#4A7CB5'
            );

            CREATE TABLE IF NOT EXISTS Subjects (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                StudentId      INTEGER NOT NULL,
                Name           TEXT NOT NULL,
                Color          TEXT NOT NULL DEFAULT '#4A7CB5',
                SortOrder      INTEGER NOT NULL DEFAULT 0,
                IsActive       INTEGER NOT NULL DEFAULT 1,
                ScheduleType   TEXT NOT NULL DEFAULT 'None',
                ScheduleDays   TEXT NOT NULL DEFAULT '',
                ScheduleDates  TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (StudentId) REFERENCES Students(Id)
            );

            CREATE TABLE IF NOT EXISTS Resources (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL DEFAULT '',
                Type        TEXT NOT NULL DEFAULT 'URL',
                Path        TEXT NOT NULL DEFAULT '',
                SubjectId   INTEGER NULL,
                Description TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS LessonEntries (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                SubjectId  INTEGER NOT NULL,
                StudentId  INTEGER NOT NULL,
                LessonDate TEXT NOT NULL,
                Title      TEXT NOT NULL DEFAULT '',
                Notes      TEXT NOT NULL DEFAULT '',
                IsComplete INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (SubjectId) REFERENCES Subjects(Id),
                FOREIGN KEY (StudentId) REFERENCES Students(Id)
            );

            CREATE TABLE IF NOT EXISTS LessonItems (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                LessonEntryId  INTEGER NOT NULL,
                Title          TEXT NOT NULL DEFAULT '',
                SubTitle       TEXT NOT NULL DEFAULT '',
                SortOrder      INTEGER NOT NULL DEFAULT 0,
                IsComplete     INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (LessonEntryId) REFERENCES LessonEntries(Id)
            );
        ";

        using var cmd = new SqliteCommand(sql, conn);
        cmd.ExecuteNonQuery();

        // Safe migration: add columns that may be missing from older schema
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleType",     "TEXT NOT NULL DEFAULT 'None'");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleDays",     "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleDates",    "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleMonthly",  "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleEndType",  "TEXT NOT NULL DEFAULT 'None'");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleEndDate",  "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleEndCount", "INTEGER NOT NULL DEFAULT 0");
        MigrateAddColumnIfMissing(conn, "Subjects", "ExcludedDates",    "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "GradeKey",         "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Students", "SchoolYear",       "TEXT NOT NULL DEFAULT ''");
        // Migrate old single-title lessons to LessonItems
        MigrateLessonTitlesToItems(conn);

        SeedDefaultTemplates(conn);
    }

    // Inserts default class templates for each grade if that grade has no entries yet.
    // Safe to call repeatedly - only inserts when the grade has zero rows.
    private static void SeedDefaultTemplates(SqliteConnection conn)
    {
        // Colors cycle through a tasteful palette
        var colors = new[]
        {
            "#5B9AD5","#E06C75","#98C379","#E5C07B",
            "#C678DD","#56B6C2","#D19A66","#61AFEF"
        };

        static string C(int i, string[] palette) => palette[i % palette.Length];

        var preK = new[] {
            "Art", "Music", "Physical Education", "Fitness and Health",
            "Phonics", "Motor Skills"
        };

        var k5 = new[] {
            "English / Language Arts", "Math", "Science", "Social Studies",
            "Art", "Music", "Physical Education", "Fitness and Health",
            "Phonics", "Technology & Digital Literacy", "Handwriting", "Reading"
        };

        var templates = new Dictionary<string, string[]>
        {
            ["PreK"] = preK,
            ["K"]    = k5,
            ["1"]    = k5,
            ["2"]    = k5,
            ["3"]    = k5,
            ["4"]    = k5,
            ["5"]    = k5,
        };

        foreach (var (grade, classes) in templates)
        {
            // Only seed if this grade has no entries at all
            using var countCmd = new SqliteCommand(
                "SELECT COUNT(*) FROM GradeClasses WHERE GradeKey = @g", conn);
            countCmd.Parameters.AddWithValue("@g", grade);
            var count = Convert.ToInt32(countCmd.ExecuteScalar());
            if (count > 0) continue;

            for (int i = 0; i < classes.Length; i++)
            {
                using var ins = new SqliteCommand(@"
                    INSERT INTO GradeClasses (GradeKey, Name, Color, ScheduleType, ScheduleDays)
                    VALUES (@g, @name, @color, 'EveryDay', '')", conn);
                ins.Parameters.AddWithValue("@g",     grade);
                ins.Parameters.AddWithValue("@name",  classes[i]);
                ins.Parameters.AddWithValue("@color", C(i, colors));
                ins.ExecuteNonQuery();
            }
        }
    }

    // For each LessonEntry with a non-empty Title but no LessonItems yet, create a LessonItem from that title.
    private static void MigrateLessonTitlesToItems(SqliteConnection conn)
    {
        using var find = new SqliteCommand(@"
            SELECT le.Id, le.Title
            FROM   LessonEntries le
            WHERE  le.Title != ''
            AND    NOT EXISTS (SELECT 1 FROM LessonItems li WHERE li.LessonEntryId = le.Id)",
            conn);
        using var reader = find.ExecuteReader();
        var toMigrate = new List<(long id, string title)>();
        while (reader.Read())
            toMigrate.Add((reader.GetInt64(0), reader.GetString(1)));
        reader.Close();

        foreach (var (id, title) in toMigrate)
        {
            using var ins = new SqliteCommand(@"
                INSERT INTO LessonItems (LessonEntryId, Title, SubTitle, SortOrder, IsComplete)
                VALUES (@eid, @t, '', 0, 0)", conn);
            ins.Parameters.AddWithValue("@eid", id);
            ins.Parameters.AddWithValue("@t",   title);
            ins.ExecuteNonQuery();
        }
    }

    private static void MigrateAddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
    {
        // PRAGMA table_info returns one row per column; if none match, add it
        using var check = new SqliteCommand($"PRAGMA table_info({table})", conn);
        using var reader = check.ExecuteReader();
        bool exists = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        if (!exists)
        {
            using var alter = new SqliteCommand($"ALTER TABLE {table} ADD COLUMN {column} {definition}", conn);
            alter.ExecuteNonQuery();
        }
    }

    // -------------------------------------------------------------------------
    // Students
    // -------------------------------------------------------------------------

    public List<Student> GetStudents()
    {
        var list = new List<Student>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand("SELECT Id, Name, Grade, Color, SchoolYear FROM Students ORDER BY Name", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Student
            {
                Id         = reader.GetInt32(0),
                Name       = reader.GetString(1),
                Grade      = reader.GetString(2),
                Color      = reader.GetString(3),
                SchoolYear = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return list;
    }

    public Student AddStudent(string name, string grade, string color, string schoolYear = "")
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            "INSERT INTO Students (Name, Grade, Color, SchoolYear) VALUES (@name, @grade, @color, @sy); SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@name",  name);
        cmd.Parameters.AddWithValue("@grade", grade);
        cmd.Parameters.AddWithValue("@color", color);
        cmd.Parameters.AddWithValue("@sy",    schoolYear);

        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new Student { Id = id, Name = name, Grade = grade, Color = color, SchoolYear = schoolYear };
    }

    public void UpdateStudent(Student student)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            "UPDATE Students SET Name = @name, Grade = @grade, Color = @color, SchoolYear = @sy WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name",  student.Name);
        cmd.Parameters.AddWithValue("@grade", student.Grade);
        cmd.Parameters.AddWithValue("@color", student.Color);
        cmd.Parameters.AddWithValue("@sy",    student.SchoolYear);
        cmd.Parameters.AddWithValue("@id",    student.Id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteStudent(int studentId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            DELETE FROM LessonEntries WHERE StudentId = @id;
            DELETE FROM Subjects WHERE StudentId = @id;
            DELETE FROM Students WHERE Id = @id;",
            conn);
        cmd.Parameters.AddWithValue("@id", studentId);
        cmd.ExecuteNonQuery();
    }

    // -------------------------------------------------------------------------
    // Subjects
    // -------------------------------------------------------------------------

    public List<Subject> GetSubjects(int studentId, bool activeOnly = true)
    {
        var list = new List<Subject>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var where = activeOnly ? "AND IsActive = 1" : "";
        using var cmd = new SqliteCommand(
            $@"SELECT Id, StudentId, Name, Color, SortOrder, IsActive,
                      ScheduleType, ScheduleDays, ScheduleDates, ScheduleMonthly,
                      ScheduleEndType, ScheduleEndDate, ScheduleEndCount, ExcludedDates,
                      GradeKey
               FROM Subjects
               WHERE StudentId = @sid {where}
               ORDER BY SortOrder, Name",
            conn);
        cmd.Parameters.AddWithValue("@sid", studentId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Subject
            {
                Id                = reader.GetInt32(0),
                StudentId         = reader.GetInt32(1),
                Name              = reader.GetString(2),
                Color             = reader.GetString(3),
                SortOrder         = reader.GetInt32(4),
                IsActive          = reader.GetInt32(5) == 1,
                ScheduleType      = reader.GetString(6),
                ScheduleDays      = reader.GetString(7),
                ScheduleDates     = reader.GetString(8),
                ScheduleMonthly   = reader.IsDBNull(9)  ? "" : reader.GetString(9),
                ScheduleEndType   = reader.IsDBNull(10) ? "None" : reader.GetString(10),
                ScheduleEndDate   = reader.IsDBNull(11) ? "" : reader.GetString(11),
                ScheduleEndCount  = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                ExcludedDates     = reader.IsDBNull(13) ? "" : reader.GetString(13),
                GradeKey          = reader.IsDBNull(14) ? "" : reader.GetString(14)
            });
        }
        return list;
    }

    public Subject AddSubject(Subject subject)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            INSERT INTO Subjects (StudentId, Name, Color, SortOrder,
                                  ScheduleType, ScheduleDays, ScheduleDates, ScheduleMonthly,
                                  ScheduleEndType, ScheduleEndDate, ScheduleEndCount, ExcludedDates,
                                  GradeKey)
            VALUES (@sid, @name, @color, @order,
                    @stype, @sdays, @sdates, @smonthly,
                    @sendtype, @senddate, @sendcount, @excl,
                    @gradekey);
            SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@sid",       subject.StudentId);
        cmd.Parameters.AddWithValue("@name",      subject.Name);
        cmd.Parameters.AddWithValue("@color",     subject.Color);
        cmd.Parameters.AddWithValue("@order",     subject.SortOrder);
        cmd.Parameters.AddWithValue("@stype",     subject.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays",     subject.ScheduleDays);
        cmd.Parameters.AddWithValue("@sdates",    subject.ScheduleDates);
        cmd.Parameters.AddWithValue("@smonthly",  subject.ScheduleMonthly);
        cmd.Parameters.AddWithValue("@sendtype",  subject.ScheduleEndType);
        cmd.Parameters.AddWithValue("@senddate",  subject.ScheduleEndDate);
        cmd.Parameters.AddWithValue("@sendcount", subject.ScheduleEndCount);
        cmd.Parameters.AddWithValue("@excl",      subject.ExcludedDates);
        cmd.Parameters.AddWithValue("@gradekey",  subject.GradeKey);

        subject.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return subject;
    }

    public void UpdateSubject(Subject subject)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            UPDATE Subjects
            SET Name = @name, Color = @color, SortOrder = @order, IsActive = @active,
                ScheduleType = @stype, ScheduleDays = @sdays, ScheduleDates = @sdates,
                ScheduleMonthly = @smonthly,
                ScheduleEndType = @sendtype, ScheduleEndDate = @senddate, ScheduleEndCount = @sendcount,
                ExcludedDates = @excl, GradeKey = @gradekey
            WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name",      subject.Name);
        cmd.Parameters.AddWithValue("@color",     subject.Color);
        cmd.Parameters.AddWithValue("@order",     subject.SortOrder);
        cmd.Parameters.AddWithValue("@active",    subject.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@stype",     subject.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays",     subject.ScheduleDays);
        cmd.Parameters.AddWithValue("@sdates",    subject.ScheduleDates);
        cmd.Parameters.AddWithValue("@smonthly",  subject.ScheduleMonthly);
        cmd.Parameters.AddWithValue("@sendtype",  subject.ScheduleEndType);
        cmd.Parameters.AddWithValue("@senddate",  subject.ScheduleEndDate);
        cmd.Parameters.AddWithValue("@sendcount", subject.ScheduleEndCount);
        cmd.Parameters.AddWithValue("@excl",      subject.ExcludedDates);
        cmd.Parameters.AddWithValue("@gradekey",  subject.GradeKey);
        cmd.Parameters.AddWithValue("@id",        subject.Id);
        cmd.ExecuteNonQuery();
    }

    // Add a single excluded date for a subject (for "delete this occurrence only")
    public void AddExcludedDate(Subject subject, string dateStr)
    {
        var dates = string.IsNullOrEmpty(subject.ExcludedDates)
            ? new List<string>()
            : subject.ExcludedDates.Split(',').Select(d => d.Trim()).ToList();
        if (!dates.Contains(dateStr))
            dates.Add(dateStr);
        subject.ExcludedDates = string.Join(",", dates);
        UpdateSubject(subject);
    }

    public void DeleteSubject(int subjectId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            DELETE FROM LessonItems WHERE LessonEntryId IN (SELECT Id FROM LessonEntries WHERE SubjectId = @id);
            DELETE FROM LessonEntries WHERE SubjectId = @id;
            DELETE FROM Subjects WHERE Id = @id;",
            conn);
        cmd.Parameters.AddWithValue("@id", subjectId);
        cmd.ExecuteNonQuery();
    }

    // -------------------------------------------------------------------------
    // Lesson Entries
    // -------------------------------------------------------------------------

    public List<LessonEntry> GetEntriesForRange(int studentId, string startDate, string endDate)
    {
        var list = new List<LessonEntry>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            SELECT Id, SubjectId, StudentId, LessonDate, Notes, IsComplete
            FROM LessonEntries
            WHERE StudentId = @sid
              AND LessonDate >= @start
              AND LessonDate <= @end
            ORDER BY LessonDate, SubjectId",
            conn);
        cmd.Parameters.AddWithValue("@sid",   studentId);
        cmd.Parameters.AddWithValue("@start", startDate);
        cmd.Parameters.AddWithValue("@end",   endDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadEntry(reader));
        reader.Close();

        // Load LessonItems for all entries in one pass
        LoadItemsForEntries(conn, list);
        return list;
    }

    public LessonEntry? GetEntry(int subjectId, int studentId, string date)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            SELECT Id, SubjectId, StudentId, LessonDate, Notes, IsComplete
            FROM LessonEntries
            WHERE SubjectId = @sub AND StudentId = @sid AND LessonDate = @date
            LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("@sub",  subjectId);
        cmd.Parameters.AddWithValue("@sid",  studentId);
        cmd.Parameters.AddWithValue("@date", date);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var entry = ReadEntry(reader);
        reader.Close();
        LoadItemsForEntries(conn, new List<LessonEntry> { entry });
        return entry;
    }

    // Inserts if Id == 0, updates otherwise. Also saves Items list.
    public LessonEntry SaveEntry(LessonEntry entry)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        if (entry.Id == 0)
        {
            using var cmd = new SqliteCommand(@"
                INSERT INTO LessonEntries (SubjectId, StudentId, LessonDate, Notes, IsComplete)
                VALUES (@sub, @sid, @date, @notes, @done);
                SELECT last_insert_rowid();",
                conn);
            cmd.Parameters.AddWithValue("@sub",   entry.SubjectId);
            cmd.Parameters.AddWithValue("@sid",   entry.StudentId);
            cmd.Parameters.AddWithValue("@date",  entry.LessonDate);
            cmd.Parameters.AddWithValue("@notes", entry.Notes);
            cmd.Parameters.AddWithValue("@done",  entry.IsComplete ? 1 : 0);
            entry.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }
        else
        {
            using var cmd = new SqliteCommand(@"
                UPDATE LessonEntries SET Notes = @notes, IsComplete = @done WHERE Id = @id",
                conn);
            cmd.Parameters.AddWithValue("@notes", entry.Notes);
            cmd.Parameters.AddWithValue("@done",  entry.IsComplete ? 1 : 0);
            cmd.Parameters.AddWithValue("@id",    entry.Id);
            cmd.ExecuteNonQuery();
        }

        // Replace all items
        using var delItems = new SqliteCommand("DELETE FROM LessonItems WHERE LessonEntryId = @eid", conn);
        delItems.Parameters.AddWithValue("@eid", entry.Id);
        delItems.ExecuteNonQuery();

        for (int i = 0; i < entry.Items.Count; i++)
        {
            var item = entry.Items[i];
            using var ins = new SqliteCommand(@"
                INSERT INTO LessonItems (LessonEntryId, Title, SubTitle, SortOrder, IsComplete)
                VALUES (@eid, @t, @st, @ord, @done);
                SELECT last_insert_rowid();",
                conn);
            ins.Parameters.AddWithValue("@eid",  entry.Id);
            ins.Parameters.AddWithValue("@t",    item.Title);
            ins.Parameters.AddWithValue("@st",   item.SubTitle);
            ins.Parameters.AddWithValue("@ord",  i);
            ins.Parameters.AddWithValue("@done", item.IsComplete ? 1 : 0);
            item.Id = Convert.ToInt32(ins.ExecuteScalar());
        }

        return entry;
    }

    // Toggle IsComplete on a single LessonItem
    public void SetLessonItemComplete(int itemId, bool complete)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = new SqliteCommand(
            "UPDATE LessonItems SET IsComplete = @done WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@done", complete ? 1 : 0);
        cmd.Parameters.AddWithValue("@id",   itemId);
        cmd.ExecuteNonQuery();
    }

    // Toggle IsComplete on a whole LessonEntry block
    public void SetEntryComplete(int entryId, bool complete)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = new SqliteCommand(
            "UPDATE LessonEntries SET IsComplete = @done WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@done", complete ? 1 : 0);
        cmd.Parameters.AddWithValue("@id",   entryId);
        cmd.ExecuteNonQuery();
    }

    // Create an entry if it doesn't exist yet (for quick-complete from calendar)
    public LessonEntry EnsureEntry(int subjectId, int studentId, string date)
    {
        var existing = GetEntry(subjectId, studentId, date);
        if (existing != null) return existing;
        return SaveEntry(new LessonEntry
        {
            SubjectId  = subjectId,
            StudentId  = studentId,
            LessonDate = date,
            Notes      = "",
            IsComplete = false
        });
    }

    public void DeleteEntry(int entryId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var delItems = new SqliteCommand("DELETE FROM LessonItems WHERE LessonEntryId = @id", conn);
        delItems.Parameters.AddWithValue("@id", entryId);
        delItems.ExecuteNonQuery();

        using var cmd = new SqliteCommand("DELETE FROM LessonEntries WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", entryId);
        cmd.ExecuteNonQuery();
    }

    private static void LoadItemsForEntries(SqliteConnection conn, List<LessonEntry> entries)
    {
        if (entries.Count == 0) return;
        var ids = string.Join(",", entries.Select(e => e.Id));
        using var cmd = new SqliteCommand(
            $"SELECT Id, LessonEntryId, Title, SubTitle, SortOrder, IsComplete FROM LessonItems WHERE LessonEntryId IN ({ids}) ORDER BY SortOrder",
            conn);
        using var r = cmd.ExecuteReader();
        var lookup = entries.ToDictionary(e => e.Id);
        while (r.Read())
        {
            var entryId = r.GetInt32(1);
            if (lookup.TryGetValue(entryId, out var entry))
            {
                entry.Items.Add(new LessonItem
                {
                    Id            = r.GetInt32(0),
                    LessonEntryId = entryId,
                    Title         = r.GetString(2),
                    SubTitle      = r.GetString(3),
                    SortOrder     = r.GetInt32(4),
                    IsComplete    = r.GetInt32(5) == 1
                });
            }
        }
    }

    private static LessonEntry ReadEntry(SqliteDataReader r) => new()
    {
        Id         = r.GetInt32(0),
        SubjectId  = r.GetInt32(1),
        StudentId  = r.GetInt32(2),
        LessonDate = r.GetString(3),
        Notes      = r.GetString(4),
        IsComplete = r.GetInt32(5) == 1
    };

    // -------------------------------------------------------------------------
    // App Settings
    // -------------------------------------------------------------------------

    public AppSettings GetSettings()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd    = new SqliteCommand("SELECT Key, Value FROM AppSettings", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            dict[reader.GetString(0)] = reader.GetString(1);

        return new AppSettings
        {
            Theme                = dict.GetValueOrDefault("Theme",               "Light"),
            CustomPrimaryColor   = dict.GetValueOrDefault("CustomPrimaryColor",  "#4A7CB5"),
            CustomSecondaryColor = dict.GetValueOrDefault("CustomSecondaryColor","#F5F6FA"),
            CustomFontColor      = dict.GetValueOrDefault("CustomFontColor",     "#1C2333"),
            FontSize             = dict.GetValueOrDefault("FontSize",            "Medium"),
            FontFamily           = dict.GetValueOrDefault("FontFamily",          "Segoe UI"),
            SchoolYearStart      = dict.GetValueOrDefault("SchoolYearStart",     DateTime.Today.ToString("yyyy-MM-dd")),
            SchoolYearEnd        = dict.GetValueOrDefault("SchoolYearEnd",       DateTime.Today.AddYears(1).AddDays(-1).ToString("yyyy-MM-dd")),
            SchoolDays               = dict.GetValueOrDefault("SchoolDays",               "1,2,3,4,5"),
            ShowGradeTemplatePrompt  = dict.GetValueOrDefault("ShowGradeTemplatePrompt",  "true") != "false",
            HasSeenWalkthrough       = dict.GetValueOrDefault("HasSeenWalkthrough",       "false") == "true",
        };
    }

    public void SaveSettings(AppSettings s)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // Upsert each setting key
        void Upsert(string key, string value)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO AppSettings (Key, Value) VALUES (@k, @v) ON CONFLICT(Key) DO UPDATE SET Value = @v",
                conn);
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        Upsert("Theme",                s.Theme);
        Upsert("CustomPrimaryColor",   s.CustomPrimaryColor);
        Upsert("CustomSecondaryColor", s.CustomSecondaryColor);
        Upsert("CustomFontColor",      s.CustomFontColor);
        Upsert("FontSize",             s.FontSize);
        Upsert("FontFamily",           s.FontFamily);
        Upsert("SchoolYearStart",      s.SchoolYearStart);
        Upsert("SchoolYearEnd",        s.SchoolYearEnd);
        Upsert("SchoolDays",               s.SchoolDays);
        Upsert("ShowGradeTemplatePrompt",  s.ShowGradeTemplatePrompt ? "true" : "false");
        Upsert("HasSeenWalkthrough",       s.HasSeenWalkthrough ? "true" : "false");
    }

    // Wipes all student/lesson data. AppSettings and GradeClasses are preserved.
    public void DeleteAllData()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var statements = new[]
        {
            "DELETE FROM LessonItems",
            "DELETE FROM LessonEntries",
            "DELETE FROM Resources",
            "DELETE FROM Subjects",
            "DELETE FROM Students"
        };

        foreach (var sql in statements)
        {
            using var cmd = new SqliteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }

    // -------------------------------------------------------------------------
    // Grade Classes (class library + template schedules)
    // -------------------------------------------------------------------------

    public List<GradeClass> GetGradeClasses(string gradeKey)
    {
        var list = new List<GradeClass>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            "SELECT Id, GradeKey, Name, Color, ScheduleType, ScheduleDays FROM GradeClasses WHERE GradeKey = @g ORDER BY Name",
            conn);
        cmd.Parameters.AddWithValue("@g", gradeKey);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new GradeClass
            {
                Id           = reader.GetInt32(0),
                GradeKey     = reader.GetString(1),
                Name         = reader.GetString(2),
                Color        = reader.GetString(3),
                ScheduleType = reader.GetString(4),
                ScheduleDays = reader.GetString(5)
            });
        }
        return list;
    }

    public GradeClass AddGradeClass(GradeClass gc)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            INSERT INTO GradeClasses (GradeKey, Name, Color, ScheduleType, ScheduleDays)
            VALUES (@g, @name, @color, @stype, @sdays);
            SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@g",     gc.GradeKey);
        cmd.Parameters.AddWithValue("@name",  gc.Name);
        cmd.Parameters.AddWithValue("@color", gc.Color);
        cmd.Parameters.AddWithValue("@stype", gc.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays", gc.ScheduleDays);
        gc.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return gc;
    }

    public void UpdateGradeClass(GradeClass gc)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            UPDATE GradeClasses
            SET Name = @name, Color = @color, ScheduleType = @stype, ScheduleDays = @sdays
            WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name",  gc.Name);
        cmd.Parameters.AddWithValue("@color", gc.Color);
        cmd.Parameters.AddWithValue("@stype", gc.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays", gc.ScheduleDays);
        cmd.Parameters.AddWithValue("@id",    gc.Id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGradeClass(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand("DELETE FROM GradeClasses WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // Applies the grade template to a student - creates a Subject for each GradeClass
    // that has a non-None schedule. Skips any class already present by name.
    public void ApplyGradeTemplate(int studentId, string gradeKey)
    {
        var templates = GetGradeClasses(gradeKey);
        var existing  = GetSubjects(studentId, activeOnly: false).Select(s => s.Name.ToLower()).ToHashSet();

        foreach (var t in templates)
        {
            if (t.ScheduleType == "None") continue;
            if (existing.Contains(t.Name.ToLower())) continue;

            AddSubject(new Subject
            {
                StudentId     = studentId,
                Name          = t.Name,
                Color         = t.Color,
                ScheduleType  = t.ScheduleType,
                ScheduleDays  = t.ScheduleDays,
                ScheduleDates = "",
                GradeKey      = gradeKey
            });
        }
    }

    // Returns distinct (student, grade) pairs for the Reports dialog picker.
    // For subjects with an empty GradeKey (pre-migration rows), we fall back to
    // the student's current grade so they still appear under a grade heading.
    // Students with no subjects at all are included using their current grade.
    public List<StudentGradeGroup> GetStudentGradePairs()
    {
        var list = new List<StudentGradeGroup>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            SELECT DISTINCT st.Id, st.Name, st.Color,
                CASE WHEN sub.GradeKey != '' THEN sub.GradeKey ELSE st.Grade END AS ResolvedGrade
            FROM Subjects sub
            JOIN Students st ON st.Id = sub.StudentId

            UNION

            SELECT st.Id, st.Name, st.Color, st.Grade
            FROM Students st
            WHERE NOT EXISTS (SELECT 1 FROM Subjects WHERE StudentId = st.Id)
              AND st.Grade != ''

            ORDER BY Name, ResolvedGrade",
            conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new StudentGradeGroup
            {
                StudentId    = reader.GetInt32(0),
                StudentName  = reader.GetString(1),
                StudentColor = reader.GetString(2),
                GradeKey     = reader.GetString(3)
            });
        }
        return list;
    }

    // Returns subjects for a specific student + grade pair.
    // Rows with an empty GradeKey fall back to the student's current grade (passed in).
    public List<Subject> GetSubjectsForGroup(int studentId, string gradeKey, string studentCurrentGrade)
    {
        var all = GetSubjects(studentId, activeOnly: false);
        return all.Where(s =>
        {
            // Resolve: use stored GradeKey if present, otherwise fall back to current grade
            var effective = string.IsNullOrEmpty(s.GradeKey) ? studentCurrentGrade : s.GradeKey;
            return effective == gradeKey;
        }).ToList();
    }

    // Returns all subjects with student name attached for a given grade key.
    // Used in the grade-level CSV roster export. Applies the same fallback for empty GradeKey.
    public List<(string StudentName, Subject Subject)> GetSubjectsByGrade(string gradeKey)
    {
        var result = new List<(string, Subject)>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            SELECT sub.Id, sub.StudentId, sub.Name, sub.Color, sub.SortOrder, sub.IsActive,
                   sub.ScheduleType, sub.ScheduleDays, sub.ScheduleDates, sub.ScheduleMonthly,
                   sub.ScheduleEndType, sub.ScheduleEndDate, sub.ScheduleEndCount,
                   sub.ExcludedDates, sub.GradeKey, st.Name AS StudentName, st.Grade AS StudentGrade
            FROM Subjects sub
            JOIN Students st ON st.Id = sub.StudentId
            ORDER BY st.Name, sub.SortOrder, sub.Name",
            conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var storedGrade    = reader.IsDBNull(14) ? "" : reader.GetString(14);
            var studentGrade   = reader.GetString(16);
            var effectiveGrade = string.IsNullOrEmpty(storedGrade) ? studentGrade : storedGrade;

            if (effectiveGrade != gradeKey) continue;

            var subject = new Subject
            {
                Id               = reader.GetInt32(0),
                StudentId        = reader.GetInt32(1),
                Name             = reader.GetString(2),
                Color            = reader.GetString(3),
                SortOrder        = reader.GetInt32(4),
                IsActive         = reader.GetInt32(5) == 1,
                ScheduleType     = reader.GetString(6),
                ScheduleDays     = reader.GetString(7),
                ScheduleDates    = reader.GetString(8),
                ScheduleMonthly  = reader.IsDBNull(9)  ? "" : reader.GetString(9),
                ScheduleEndType  = reader.IsDBNull(10) ? "None" : reader.GetString(10),
                ScheduleEndDate  = reader.IsDBNull(11) ? "" : reader.GetString(11),
                ScheduleEndCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                ExcludedDates    = reader.IsDBNull(13) ? "" : reader.GetString(13),
                GradeKey         = storedGrade
            };
            result.Add((reader.GetString(15), subject));
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Resources
    // -------------------------------------------------------------------------

    public List<Resource> GetResources(int? subjectId = null)
    {
        var list = new List<Resource>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var where = subjectId.HasValue ? "WHERE r.SubjectId = @sid" : "";
        var sql = $@"
            SELECT r.Id, r.Name, r.Type, r.Path, r.SubjectId, r.Description,
                   COALESCE(s.Name, '') AS SubjectName
            FROM Resources r
            LEFT JOIN Subjects s ON s.Id = r.SubjectId
            {where}
            ORDER BY r.Name";

        using var cmd = new SqliteCommand(sql, conn);
        if (subjectId.HasValue) cmd.Parameters.AddWithValue("@sid", subjectId.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Resource
            {
                Id          = reader.GetInt32(0),
                Name        = reader.GetString(1),
                Type        = reader.GetString(2),
                Path        = reader.GetString(3),
                SubjectId   = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Description = reader.GetString(5),
                SubjectName = reader.GetString(6)
            });
        }
        return list;
    }

    public Resource AddResource(Resource r)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            INSERT INTO Resources (Name, Type, Path, SubjectId, Description)
            VALUES (@name, @type, @path, @sid, @desc);
            SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@name", r.Name);
        cmd.Parameters.AddWithValue("@type", r.Type);
        cmd.Parameters.AddWithValue("@path", r.Path);
        cmd.Parameters.AddWithValue("@sid",  (object?)r.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", r.Description);
        r.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return r;
    }

    public void UpdateResource(Resource r)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            UPDATE Resources
            SET Name = @name, Type = @type, Path = @path, SubjectId = @sid, Description = @desc
            WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name", r.Name);
        cmd.Parameters.AddWithValue("@type", r.Type);
        cmd.Parameters.AddWithValue("@path", r.Path);
        cmd.Parameters.AddWithValue("@sid",  (object?)r.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", r.Description);
        cmd.Parameters.AddWithValue("@id",   r.Id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteResource(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand("DELETE FROM Resources WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}
