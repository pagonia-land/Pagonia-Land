using System.Globalization;

namespace PagoniaLand.Paker;

/// <summary>
/// Parses the filter flags <c>-c</c>/<c>--compress</c>, <c>-d</c>/<c>--decompress</c>,
/// <c>-s|--start=&lt;n&gt;</c>, <c>-e|--end=&lt;n&gt;</c>, and <c>-f|--filter=&lt;substr&gt;</c>
/// out of an argv-style array, returning a <see cref="PakFilter"/> plus the
/// remaining positional arguments in their original order. Both
/// <c>--name=value</c> and <c>--name value</c> are accepted.
/// </summary>
public static class FilterArgumentParser
{
    public sealed record Result(
        PakFilter Filter,
        string? JsonReportPath,
        int? Jobs,
        IReadOnlyList<string> Deletions,
        bool NoGdBinRegister,
        IReadOnlyList<string> Positional,
        string? Error)
    {
        public bool Success => Error is null;
    }

    public static Result Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var positional = new List<string>(args.Count);
        var deletions = new List<string>();
        var compressedOnly = false;
        var uncompressedOnly = false;
        int? start = null;
        int? end = null;
        string? filenameContains = null;
        string? jsonReportPath = null;
        int? jobs = null;
        var noGdBinRegister = false;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];

            switch (token)
            {
                case "-c" or "--compress":
                    compressedOnly = true;
                    continue;
                case "-d" or "--decompress":
                    uncompressedOnly = true;
                    continue;
                case "--no-gdbin-register":
                    noGdBinRegister = true;
                    continue;
            }

            if (TryConsumeIntFlag(args, ref i, token, "-s", "--start", out var sValue, out var sError))
            {
                if (sError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,sError);
                start = sValue;
                continue;
            }

            if (TryConsumeIntFlag(args, ref i, token, "-e", "--end", out var eValue, out var eError))
            {
                if (eError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,eError);
                end = eValue;
                continue;
            }

            if (TryConsumeStringFlag(args, ref i, token, "-f", "--filter", out var fValue, out var fError))
            {
                if (fError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,fError);
                filenameContains = fValue;
                continue;
            }

            if (TryConsumeStringFlag(args, ref i, token, shortName: null, "--json", out var jValue, out var jError))
            {
                if (jError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,jError);
                jsonReportPath = jValue;
                continue;
            }

            if (TryConsumeIntFlag(args, ref i, token, "-j", "--jobs", out var jobsValue, out var jobsError))
            {
                if (jobsError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,jobsError);
                if (jobsValue < 1) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,$"--jobs must be at least 1 (got {jobsValue}).");
                jobs = jobsValue;
                continue;
            }

            if (TryConsumeStringFlag(args, ref i, token, shortName: null, "--delete", out var delValue, out var delError))
            {
                if (delError is not null) return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,delError);
                if (!string.IsNullOrWhiteSpace(delValue)) deletions.Add(delValue);
                continue;
            }

            positional.Add(token);
        }

        if (compressedOnly && uncompressedOnly)
        {
            return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,"Flags --compress and --decompress are mutually exclusive.");
        }

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            return new Result(PakFilter.All, jsonReportPath, jobs, deletions, noGdBinRegister, positional,$"--start ({start.Value}) must not be greater than --end ({end.Value}).");
        }

        var filter = new PakFilter(compressedOnly, uncompressedOnly, start, end, filenameContains);
        return new Result(filter, jsonReportPath, jobs, deletions, noGdBinRegister, positional, Error: null);
    }

    private static bool TryConsumeIntFlag(
        IReadOnlyList<string> args, ref int i, string token,
        string? shortName, string longName,
        out int value, out string? error)
    {
        if (!TryConsumeStringFlag(args, ref i, token, shortName, longName, out var raw, out error))
        {
            value = default;
            return false;
        }
        if (error is not null)
        {
            value = default;
            return true;
        }
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            error = $"Flag {longName} expects a non-negative integer (got '{raw}').";
            return true;
        }
        return true;
    }

    private static bool TryConsumeStringFlag(
        IReadOnlyList<string> args, ref int i, string token,
        string? shortName, string longName,
        out string? value, out string? error)
    {
        value = null;
        error = null;

        if ((shortName is not null && token == shortName) || token == longName)
        {
            if (i + 1 >= args.Count)
            {
                error = $"Flag {token} requires a value.";
                return true;
            }
            i++;
            value = args[i];
            return true;
        }

        var longEq = longName + "=";
        if (token.StartsWith(longEq, StringComparison.Ordinal))
        {
            value = token[longEq.Length..];
            return true;
        }

        if (shortName is not null)
        {
            var shortEq = shortName + "=";
            if (token.StartsWith(shortEq, StringComparison.Ordinal))
            {
                value = token[shortEq.Length..];
                return true;
            }
        }

        return false;
    }
}
