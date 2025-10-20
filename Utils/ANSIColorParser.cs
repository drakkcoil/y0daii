using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Y0daiiIRC.Utils
{
    public static class ANSIColorParser
    {
        private static readonly Dictionary<int, Color> AnsiColors = new()
        {
            { 0, Colors.White },      // White/Default
            { 1, Colors.Black },      // Black
            { 2, Colors.Navy },       // Dark Blue
            { 3, Colors.Green },      // Dark Green
            { 4, Colors.Red },        // Dark Red
            { 5, Colors.Maroon },     // Brown
            { 6, Colors.Purple },     // Purple
            { 7, Colors.Orange },     // Orange
            { 8, Colors.Yellow },     // Yellow
            { 9, Colors.Lime },       // Light Green
            { 10, Colors.Teal },      // Cyan
            { 11, Colors.LightBlue }, // Light Cyan
            { 12, Colors.Blue },      // Light Blue
            { 13, Colors.Magenta },   // Light Magenta
            { 14, Colors.Gray },      // Gray
            { 15, Colors.LightGray }  // Light Gray
        };

        public static List<FormattedText> ParseANSIText(string text)
        {
            var result = new List<FormattedText>();
            if (string.IsNullOrEmpty(text)) return result;

            var currentColor = Colors.White;
            var currentBackground = Colors.Transparent;
            var isBold = false;
            var isUnderline = false;
            var isItalic = false;

            var parts = Regex.Split(text, @"(\x1b\[[0-9;]*m)");
            var currentText = new StringBuilder();

            foreach (var part in parts)
            {
                if (part.StartsWith("\x1b[") && part.EndsWith("m"))
                {
                    // Process accumulated text
                    if (currentText.Length > 0)
                    {
                        result.Add(new FormattedText
                        {
                            Text = currentText.ToString(),
                            Foreground = currentColor,
                            Background = currentBackground,
                            IsBold = isBold,
                            IsUnderline = isUnderline,
                            IsItalic = isItalic
                        });
                        currentText.Clear();
                    }

                    // Parse ANSI codes
                    var codes = part.Substring(2, part.Length - 3); // Remove \x1b[ and m
                    if (string.IsNullOrEmpty(codes))
                    {
                        // Reset all formatting
                        currentColor = Colors.White;
                        currentBackground = Colors.Transparent;
                        isBold = false;
                        isUnderline = false;
                        isItalic = false;
                        continue;
                    }

                    var codeArray = codes.Split(';');
                    foreach (var code in codeArray)
                    {
                        if (int.TryParse(code, out int codeValue))
                        {
                            switch (codeValue)
                            {
                                case 0: // Reset
                                    currentColor = Colors.White;
                                    currentBackground = Colors.Transparent;
                                    isBold = false;
                                    isUnderline = false;
                                    isItalic = false;
                                    break;
                                case 1: // Bold
                                    isBold = true;
                                    break;
                                case 3: // Italic
                                    isItalic = true;
                                    break;
                                case 4: // Underline
                                    isUnderline = true;
                                    break;
                                case 22: // Normal intensity
                                    isBold = false;
                                    break;
                                case 23: // Not italic
                                    isItalic = false;
                                    break;
                                case 24: // Not underlined
                                    isUnderline = false;
                                    break;
                                case 30: case 31: case 32: case 33: case 34: case 35: case 36: case 37: // Foreground colors
                                    currentColor = AnsiColors.GetValueOrDefault(codeValue - 30, Colors.White);
                                    break;
                                case 40: case 41: case 42: case 43: case 44: case 45: case 46: case 47: // Background colors
                                    currentBackground = AnsiColors.GetValueOrDefault(codeValue - 40, Colors.Transparent);
                                    break;
                                case 90: case 91: case 92: case 93: case 94: case 95: case 96: case 97: // Bright foreground colors
                                    currentColor = AnsiColors.GetValueOrDefault(codeValue - 82, Colors.White);
                                    break;
                                case 100: case 101: case 102: case 103: case 104: case 105: case 106: case 107: // Bright background colors
                                    currentBackground = AnsiColors.GetValueOrDefault(codeValue - 92, Colors.Transparent);
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    currentText.Append(part);
                }
            }

            // Add remaining text
            if (currentText.Length > 0)
            {
                result.Add(new FormattedText
                {
                    Text = currentText.ToString(),
                    Foreground = currentColor,
                    Background = currentBackground,
                    IsBold = isBold,
                    IsUnderline = isUnderline,
                    IsItalic = isItalic
                });
            }

            return result;
        }

        public static string StripANSICodes(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
        }
    }

    public class FormattedText
    {
        public string Text { get; set; } = string.Empty;
        public Color Foreground { get; set; } = Colors.White;
        public Color Background { get; set; } = Colors.Transparent;
        public bool IsBold { get; set; }
        public bool IsUnderline { get; set; }
        public bool IsItalic { get; set; }
    }
}
