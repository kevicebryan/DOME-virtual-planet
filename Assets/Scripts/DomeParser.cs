using System;
using System.Collections.Generic;

public class DomeParser
{
    public static DomeCommand Parse(string input)
    {
        var identifier = "";
        var command = new DomeCommand("ReportStatus");
        foreach (var c in input.ToCharArray())
        {
            switch (c)
            {
                case '(':
                    command = new DomeCommand(identifier);
                    identifier = "";
                    continue;
                case ')':
                case ',':
                    command.Arguments.Add(identifier);
                    identifier = "";
                    continue;
                case ' ':
                    continue;
            }

            identifier += c;
        }

        return command;
    }
}