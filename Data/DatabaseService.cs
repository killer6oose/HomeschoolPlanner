using System.IO;
using HomeschoolPlanner.Models;
using Microsoft.Data.Sqlite;

namespace HomeschoolPlanner.Data;

// Single entry point for all database access.
// The .db file lives next to the .exe so the app is fully self-contained.
public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;
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
        ";

        using var cmd = new SqliteCommand(sql, conn);
        cmd.ExecuteNonQuery();

        // Safe migration: add schedule columns if upgrading from an older schema
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleType",  "TEXT NOT NULL DEFAULT 'None'");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleDays",  "TEXT NOT NULL DEFAULT ''");
        MigrateAddColumnIfMissing(conn, "Subjects", "ScheduleDates", "TEXT NOT NULL DEFAULT ''");
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

        using var cmd = new SqliteCommand("SELECT Id, Name, Grade, Color FROM Students ORDER BY Name", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Student
            {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Grade = reader.GetString(2),
                Color = reader.GetString(3)
            });
        }
        return list;
    }

    public Student AddStudent(string name, string grade, string color)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            "INSERT INTO Students (Name, Grade, Color) VALUES (@name, @grade, @color); SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@grade", grade);
        cmd.Parameters.AddWithValue("@color", color);

        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new Student { Id = id, Name = name, Grade = grade, Color = color };
    }

    public void UpdateStudent(Student student)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            "UPDATE Students SET Name = @name, Grade = @grade, Color = @color WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name",  student.Name);
        cmd.Parameters.AddWithValue("@grade", student.Grade);
        cmd.Parameters.AddWithValue("@color", student.Color);
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
            $@"SELECT Id, StudentId, Name, Color, SortOrder, IsActive, ScheduleType, ScheduleDays, ScheduleDates
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
                Id            = reader.GetInt32(0),
                StudentId     = reader.GetInt32(1),
                Name          = reader.GetString(2),
                Color         = reader.GetString(3),
                SortOrder     = reader.GetInt32(4),
                IsActive      = reader.GetInt32(5) == 1,
                ScheduleType  = reader.GetString(6),
                ScheduleDays  = reader.GetString(7),
                ScheduleDates = reader.GetString(8)
            });
        }
        return list;
    }

    public Subject AddSubject(Subject subject)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            INSERT INTO Subjects (StudentId, Name, Color, SortOrder, ScheduleType, ScheduleDays, ScheduleDates)
            VALUES (@sid, @name, @color, @order, @stype, @sdays, @sdates);
            SELECT last_insert_rowid();",
            conn);
        cmd.Parameters.AddWithValue("@sid",    subject.StudentId);
        cmd.Parameters.AddWithValue("@name",   subject.Name);
        cmd.Parameters.AddWithValue("@color",  subject.Color);
        cmd.Parameters.AddWithValue("@order",  subject.SortOrder);
        cmd.Parameters.AddWithValue("@stype",  subject.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays",  subject.ScheduleDays);
        cmd.Parameters.AddWithValue("@sdates", subject.ScheduleDates);

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
                ScheduleType = @stype, ScheduleDays = @sdays, ScheduleDates = @sdates
            WHERE Id = @id",
            conn);
        cmd.Parameters.AddWithValue("@name",   subject.Name);
        cmd.Parameters.AddWithValue("@color",  subject.Color);
        cmd.Parameters.AddWithValue("@order",  subject.SortOrder);
        cmd.Parameters.AddWithValue("@active", subject.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@stype",  subject.ScheduleType);
        cmd.Parameters.AddWithValue("@sdays",  subject.ScheduleDays);
        cmd.Parameters.AddWithValue("@sdates", subject.ScheduleDates);
        cmd.Parameters.AddWithValue("@id",     subject.Id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteSubject(int subjectId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
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
            SELECT Id, SubjectId, StudentId, LessonDate, Title, Notes, IsComplete
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
        {
            list.Add(ReadEntry(reader));
        }
        return list;
    }

    public LessonEntry? GetEntry(int subjectId, int studentId, string date)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            SELECT Id, SubjectId, StudentId, LessonDate, Title, Notes, IsComplete
            FROM LessonEntries
            WHERE SubjectId = @sub AND StudentId = @sid AND LessonDate = @date
            LIMIT 1",
            conn);
        cmd.Parameters.AddWithValue("@sub",  subjectId);
        cmd.Parameters.AddWithValue("@sid",  studentId);
        cmd.Parameters.AddWithValue("@date", date);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    // Inserts if Id == 0, updates otherwise
    public LessonEntry SaveEntry(LessonEntry entry)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        if (entry.Id == 0)
        {
            using var cmd = new SqliteCommand(@"
                INSERT INTO LessonEntries (SubjectId, StudentId, LessonDate, Title, Notes, IsComplete)
                VALUES (@sub, @sid, @date, @title, @notes, @done);
                SELECT last_insert_rowid();",
                conn);
            cmd.Parameters.AddWithValue("@sub",   entry.SubjectId);
            cmd.Parameters.AddWithValue("@sid",   entry.StudentId);
            cmd.Parameters.AddWithValue("@date",  entry.LessonDate);
            cmd.Parameters.AddWithValue("@title", entry.Title);
            cmd.Parameters.AddWithValue("@notes", entry.Notes);
            cmd.Parameters.AddWithValue("@done",  entry.IsComplete ? 1 : 0);
            entry.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }
        else
        {
            using var cmd = new SqliteCommand(@"
                UPDATE LessonEntries
                SET Title = @title, Notes = @notes, IsComplete = @done
                WHERE Id = @id",
                conn);
            cmd.Parameters.AddWithValue("@title", entry.Title);
            cmd.Parameters.AddWithValue("@notes", entry.Notes);
            cmd.Parameters.AddWithValue("@done",  entry.IsComplete ? 1 : 0);
            cmd.Parameters.AddWithValue("@id",    entry.Id);
            cmd.ExecuteNonQuery();
        }

        return entry;
    }

    public void DeleteEntry(int entryId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand("DELETE FROM LessonEntries WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", entryId);
        cmd.ExecuteNonQuery();
    }

    private static LessonEntry ReadEntry(SqliteDataReader r) => new()
    {
        Id         = r.GetInt32(0),
        SubjectId  = r.GetInt32(1),
        StudentId  = r.GetInt32(2),
        LessonDate = r.GetString(3),
        Title      = r.GetString(4),
        Notes      = r.GetString(5),
        IsComplete = r.GetInt32(6) == 1
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
            Theme               = dict.GetValueOrDefault("Theme",               "Light"),
            CustomPrimaryColor  = dict.GetValueOrDefault("CustomPrimaryColor",  "#4A7CB5"),
            CustomSecondaryColor= dict.GetValueOrDefault("CustomSecondaryColor","#F5F6FA"),
            CustomFontColor     = dict.GetValueOrDefault("CustomFontColor",     "#1C2333"),
            FontSize            = dict.GetValueOrDefault("FontSize",            "Medium"),
            FontFamily          = dict.GetValueOrDefault("FontFamily",          "Segoe UI"),
            SchoolYearStart     = dict.GetValueOrDefault("SchoolYearStart",     DateTime.Today.ToString("yyyy-MM-dd")),
            SchoolDays          = dict.GetValueOrDefault("SchoolDays",          "1,2,3,4,5"),
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
        Upsert("SchoolDays",           s.SchoolDays);
    }

    // Wipes all student/lesson data. AppSettings and GradeClasses are preserved.
    public void DeleteAllData()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(@"
            DELETE FROM LessonEntries;
            DELETE FROM Subjects;
            DELETE FROM Students;",
            conn);
        cmd.ExecuteNonQuery();
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
                ScheduleDates = ""
            });
        }
    }
}
