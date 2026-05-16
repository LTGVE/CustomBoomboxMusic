using System.Collections.Generic;

internal static class StringUtils {
    internal static string[] ToCommandLineArgs(this string str)
    {
        var group = str.Split(" ");
        bool combining = false;
        var combinedString = "";
        var fullList = new List<string>();
        for (int i = 0; i < group.Length; i++)
        {
            var value = group[i];
            if (!combining)
            {
                if (value.StartsWith("\"") && !value.EndsWith("\""))
                {
                    combining = true;
                    combinedString = "";
                    combinedString += value;
                    continue;
                }
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.RemoveStartWith("\"");
                    value = value.RemoveEndWith("\"");
                }
                fullList.Add(value);
            }
            else
            {
                if (value.EndsWith("\"") && !value.StartsWith("\""))
                {
                    combining = false;
                    combinedString += " " + value;
                    combinedString = combinedString.RemoveEndWith("\"");
                    combinedString = combinedString.RemoveStartWith("\"");
                    fullList.Add(combinedString);
                    continue;
                }
                combinedString += " " + value;
            }
        }
        return fullList.ToArray();
    }
    internal static string RemoveEndWith(this string str, string endStr)
    {
        if (str.EndsWith(endStr))
        {
            return str.Substring(0, str.Length - endStr.Length);
        }
        return str;
    }
    internal static string RemoveStartWith(this string str, string startStr)
    {
        if (str.StartsWith(startStr))
        {
            return str.Substring(startStr.Length, str.Length - startStr.Length);
        }
        return str;
    }
}