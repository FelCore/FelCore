// Copyright (c) 2013-2018 Cemalettin Dervis, MIT License.
// https://github.com/cemdervis/SharpConfig

using System;
using System.IO;
using System.Text;

namespace SharpConfig
{
  internal static class ConfigurationReader
  {
    internal static Configuration ReadFromString(string source)
    {
      var config = new Configuration();

      using (var reader = new StringReader(source))
      {
        Parse(reader, config);
      }

      return config;
    }

    private static void Parse(StringReader reader, Configuration config)
    {
      var currentSection = new Section(Section.DefaultSectionName);
      var preCommentBuilder = new StringBuilder();

      int lineNumber = 0;

      string line;

      // Read until EOF.
      while ((line = reader.ReadLine()) != null)
      {
        lineNumber++;

        // Remove all leading/trailing white-spaces.
        line = line.Trim();

        // Do not process empty lines.
        if (string.IsNullOrEmpty(line))
          continue;

        var comment = ParseComment(line, out int commentIndex);

        if (commentIndex == 0)
        {
          // pre-comment
          if (!Configuration.IgnorePreComments)
            preCommentBuilder.AppendLine(comment);

          continue;
        }

        string lineWithoutComment = line;
        if (commentIndex > 0)
          lineWithoutComment = line.Remove(commentIndex).Trim(); // remove inline comment

        if (lineWithoutComment.StartsWith("[")) // Section
        {
          // If the first section has been found but settings already exist, add them to the default section.
          if (currentSection.Name == Section.DefaultSectionName && currentSection.SettingCount > 0)
            config.mSections.Add(currentSection);

          currentSection = ParseSection(lineWithoutComment, lineNumber);

          if (!Configuration.IgnoreInlineComments)
            currentSection.Comment = comment;

          if (!Configuration.IgnorePreComments && preCommentBuilder.Length > 0)
          {
            // Set the current section's pre-comment, removing the last newline character.
            currentSection.PreComment = preCommentBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
            preCommentBuilder.Length = 0; // Clear the SB - With .NET >= 4.0: preCommentBuilder.Clear()
          }

          config.mSections.Add(currentSection);
        }
        else // Setting
        {
          var setting = ParseSetting(Configuration.IgnoreInlineComments ? line : lineWithoutComment, lineNumber);

          if (!Configuration.IgnoreInlineComments)
            setting.Comment = comment;

          if (!Configuration.IgnorePreComments && preCommentBuilder.Length > 0)
          {
            // Set the setting's pre-comment, removing the last newline character.
            setting.PreComment = preCommentBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
            preCommentBuilder.Length = 0; // Clear the SB - With .NET >= 4.0: preCommentBuilder.Clear()
          }

          currentSection.Add(setting);
        }
      }
    }

    private static string ParseComment(string line, out int commentCharIndex)
    {
      // A comment starts with a valid comment character that:
      // 1. is not within a quote (eg. "this is # not a comment"), and
      // 2. is not escaped (eg. this is \# not a comment either).
      //
      // A quote has two quotation marks, neither of which is escaped.
      // For example: "this is a quote \" with an escaped quotation mark inside of it"

      string comment = null;
      commentCharIndex = -1;

      var index = 0;
      var quoteCount = 0;
      while (line.Length > index) // traverse line from left to right
      {
        var isValidCommentChar = Array.IndexOf(Configuration.ValidCommentChars, line[index]) > -1;
        var isQuotationMark = line[index] == '\"';
        var isCharWithinQuotes = quoteCount % 2 == 1;
        var isCharEscaped = index > 0 && line[index - 1] == '\\';

        if (isValidCommentChar && !isCharWithinQuotes && !isCharEscaped)
          break; // a comment has started

        if (isQuotationMark && !isCharEscaped)
          quoteCount++; // a non-escaped quotation mark has been found

        index++;
      }

      if (index < line.Length)
      {
        // The end of the string has not been reached => index points to a valid comment character.
        commentCharIndex = index;
        comment = line.Substring(index + 1).TrimStart();
      }

      return comment;
    }

    private static Section ParseSection(string line, int lineNumber)
    {
      // Format(s) of a section:
      // 1) [<name>]
      //      <name> may contain any char, including '[', ']', and a valid comment delimiter character

      int closingBracketIndex = line.LastIndexOf(']');
      if (closingBracketIndex < 0)
        throw new ParserException("closing bracket missing.", lineNumber);

      string sectionName = line.Substring(1, closingBracketIndex - 1).Trim();

      // Anything after the (last) closing bracket must be whitespace.
      if (line.Length > closingBracketIndex + 1)
      {
        var endPart = line.Substring(closingBracketIndex + 1).Trim();

        if (endPart.Length > 0)
          throw new ParserException($"unexpected token: '{endPart}'", lineNumber);
      }

      return new Section(sectionName);
    }

    private static Setting ParseSetting(string line, int lineNumber)
    {
      // Format(s) of a setting:
      // 1) <name> = <value>
      //      <name> may not contain a '='
      // 2) "<name>" = <value>
      //      <name> may contain any char, including '='

      string settingName = null;
      int equalSignIndex;

      // Parse the name first.
      bool isQuotedName = line.StartsWith("\"");
      if (isQuotedName)
      {
        // Format 2
        int closingQuoteIndex = 0;
        do
        {
          closingQuoteIndex = line.IndexOf('\"', closingQuoteIndex + 1);
        }
        while (closingQuoteIndex > 0 && line[closingQuoteIndex - 1] == '\\');

        if (closingQuoteIndex < 0)
          throw new ParserException("closing quote mark expected.", lineNumber);

        // Don't trim the name. Quoted names should be taken verbatim.
        settingName = line.Substring(1, closingQuoteIndex - 1);

        equalSignIndex = line.IndexOf('=', closingQuoteIndex + 1);
      }
      else
      {
        // Format 1
        equalSignIndex = line.IndexOf('=');
      }

      if (equalSignIndex < 0)
        throw new ParserException("setting assignment expected.", lineNumber);

      if (!isQuotedName)
        settingName = line.Substring(0, equalSignIndex).Trim();

      if (string.IsNullOrEmpty(settingName))
        throw new ParserException("setting name expected.", lineNumber);

      var settingValue = line.Substring(equalSignIndex + 1).Trim();

      return new Setting(settingName, settingValue);
    }

    internal static Configuration ReadFromBinaryStream(Stream stream, BinaryReader reader)
    {
      if (stream == null)
        throw new ArgumentNullException("stream");

      if (reader == null)
        reader = new BinaryReader(stream);

      var config = new Configuration();

      int sectionCount = reader.ReadInt32();

      for (int i = 0; i < sectionCount; ++i)
      {
        string sectionName = reader.ReadString();
        int settingCount = reader.ReadInt32();

        var section = new Section(sectionName);

        ReadCommentsBinary(reader, section);

        for (int j = 0; j < settingCount; j++)
        {
          var setting = new Setting(reader.ReadString())
          {
            RawValue = reader.ReadString()
          };

          ReadCommentsBinary(reader, setting);

          section.Add(setting);
        }

        config.Add(section);
      }

      return config;
    }

    private static void ReadCommentsBinary(BinaryReader reader, ConfigurationElement element)
    {
      bool hasComment = reader.ReadBoolean();
      if (hasComment)
      {
        // Read the comment char, but don't do anything with it.
        // This is just for backwards-compatibility.
        reader.ReadChar();
        element.Comment = reader.ReadString();
      }

      bool hasPreComment = reader.ReadBoolean();
      if (hasPreComment)
      {
        // Same as above.
        reader.ReadChar();
        element.PreComment = reader.ReadString();
      }
    }
  }
}
