using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Research log: one CSV row per snap, appended to a file in Application.persistentDataPath, so sessions and
/// participants accumulate in one table. Numbers use the invariant culture (dot decimals) whatever the system
/// locale is, so the file opens the same way in Excel, R or pandas. Writing never throws: a failure is logged once
/// and logging stops for the session.
/// </summary>
public class AssemblyLog
{
    public struct SnapRow
    {
        public string recipe, movingPart, partner;
        public Axis insertionAxis;
        public float sinceFirstGrab;   // seconds, -1 if the part was never grabbed (e.g. it was the stationary one)
        public int attempts;
        public float lateral, axial, angleError;
        public float lateralOfCorridor; // lateral / corridor radius, 0..1
        public bool axisModeUsed;
        public Axis axisKey;           // key held at the moment of the snap (None if none)
        public bool recipeCompleted;
        public string assist;          // "none" for a plain snap, "magnet" when the magnet pulled it in
    }

    // Bump when Columns change: a file with an older header is left alone and rows go to <name>_v<Version>.csv.
    const int Version = 2;

    static readonly string[] Columns =
    {
        "timestamp_utc", "session", "participant", "scene_time_s", "recipe", "moving_part", "partner", "insertion_axis",
        "time_since_first_grab_s", "grab_attempts", "lateral_offset_m", "lateral_offset_of_corridor", "axial_offset_m",
        "angle_error_deg", "axis_mode_used", "axis_key", "recipe_completed", "assist",
    };

    public string Path { get; }
    readonly string _session;
    readonly string _participant;
    bool _failed;

    public AssemblyLog(string fileName, string participant)
    {
        _session = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _participant = participant ?? "";
        string dir = Application.persistentDataPath;
        if (string.IsNullOrEmpty(fileName)) fileName = "assembly_log.csv";
        Path = System.IO.Path.Combine(dir, fileName);

        // Never append rows with a different column layout to an existing file: start a new one beside it.
        if (!HeaderMatches(Path))
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(fileName), ext = System.IO.Path.GetExtension(fileName);
            string next = System.IO.Path.Combine(dir, $"{stem}_v{Version}{ext}");
            if (!HeaderMatches(next)) next = System.IO.Path.Combine(dir, $"{stem}_v{Version}_{_session}{ext}");
            Debug.Log($"AssemblyLog: {Path} has an older column layout, so this session writes to {next}.");
            Path = next;
        }
    }

    public void Write(SnapRow r)
    {
        if (_failed) return;
        var line = new StringBuilder();
        Append(line, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        Append(line, _session);
        Append(line, _participant);
        Append(line, Time.time);
        Append(line, r.recipe);
        Append(line, r.movingPart);
        Append(line, r.partner);
        Append(line, r.insertionAxis.ToString());
        Append(line, r.sinceFirstGrab);
        Append(line, r.attempts.ToString(CultureInfo.InvariantCulture));
        Append(line, r.lateral);
        Append(line, r.lateralOfCorridor);
        Append(line, r.axial);
        Append(line, r.angleError);
        Append(line, r.axisModeUsed ? "yes" : "no");
        Append(line, r.axisKey == Axis.None ? "" : r.axisKey.ToString());
        Append(line, r.recipeCompleted ? "yes" : "no");
        Append(line, string.IsNullOrEmpty(r.assist) ? "none" : r.assist, last: true);

        try
        {
            bool isNew = !File.Exists(Path);
            using (var writer = new StreamWriter(Path, append: true, new UTF8Encoding(false)))
            {
                if (isNew) writer.WriteLine(string.Join(",", Columns));
                writer.WriteLine(line.ToString());
            }
        }
        catch (Exception e)
        {
            _failed = true;
            Debug.LogWarning($"AssemblyLog: cannot write {Path}, logging is off for this session. {e.Message}");
        }
    }

    /// <summary>True if the file does not exist yet or starts with the current header.</summary>
    static bool HeaderMatches(string path)
    {
        try
        {
            if (!File.Exists(path)) return true;
            using (var reader = new StreamReader(path)) return reader.ReadLine() == string.Join(",", Columns);
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void Append(StringBuilder line, float value) =>
        Append(line, value.ToString("0.#####", CultureInfo.InvariantCulture));

    static void Append(StringBuilder line, string value, bool last = false)
    {
        value = value ?? "";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        line.Append(value);
        if (!last) line.Append(',');
    }
}
