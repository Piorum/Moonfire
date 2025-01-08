using System.Text.RegularExpressions;

namespace FuncExt;

public static partial class Reg
{
    [GeneratedRegex(@"\d+")]
    public static partial Regex NumericRegex();
}
