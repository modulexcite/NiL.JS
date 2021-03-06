﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Core.Modules;

namespace NiL.JS.BaseLibrary
{
    /// <summary>
    /// Представляет реализация встроенного объекта JSON. Позволяет производить сериализацию и десериализацию объектов JavaScript.
    /// </summary>
    public static class JSON
    {
        private enum ParseState
        {
            Value,
            Name,
            Object,
            Array,
            End
        }

        private class StackFrame
        {
            public JSObject container;
            public JSObject value;
            public JSObject fieldName;
            public int valuesCount;
            public ParseState state;
        }

        [DoNotEnumerate]
        [ArgumentsLength(2)]
        public static JSObject parse(Arguments args)
        {
            var length = Tools.JSObjectToInt32(args.length);
            var code = args[0].ToString();
            Function reviewer = length > 1 ? args[1].oValue as Function : null;
            return parse(code, reviewer);
        }

        [Hidden]
        public static JSObject parse(string code)
        {
            return parse(code, null);
        }

        private static bool isSpace(char c)
        {
            return c != '\u000b'
                && c != '\u000c'
                && c != '\u00a0'
                && c != '\u1680'
                && c != '\u180e'
                && c != '\u2000'
                && c != '\u2001'
                && c != '\u2002'
                && c != '\u2003'
                && c != '\u2004'
                && c != '\u2005'
                && c != '\u2006'
                && c != '\u2007'
                && c != '\u2008'
                && c != '\u2009'
                && c != '\u200a'
                && c != '\u2028'
                && c != '\u2029'
                && c != '\u202f'
                && c != '\u205f'
                && c != '\u3000'
                && char.IsWhiteSpace(c);
        }

        [Hidden]
        public static JSObject parse(string code, Function reviewer)
        {
            Stack<StackFrame> stack = new Stack<StackFrame>();
            Arguments revargs = reviewer != null ? new Arguments() { length = 2 } : null;
            stack.Push(new StackFrame() { container = null, value = null, state = ParseState.Value });
            int pos = 0;
            while (code.Length > pos && isSpace(code[pos]))
                pos++;
            while (pos < code.Length)
            {
                int start = pos;
                if (char.IsDigit(code[start]) || (code[start] == '-' && char.IsDigit(code[start + 1])))
                {
                    if (stack.Peek().state != ParseState.Value)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    double value;
                    if (!Tools.ParseNumber(code, ref pos, out value))
                        throw new JSException((new SyntaxError("Invalid number definition.")));
                    var v = stack.Peek();
                    v.state = ParseState.End;
                    v.value = value;
                }
                else if (code[start] == '"')
                {
                    Parser.ValidateString(code, ref pos, true);
                    string value = code.Substring(start + 1, pos - start - 2);
                    for (var i = value.Length; i-- > 0; )
                    {
                        if ((value[i] >= 0 && value[i] <= 0x1f))
                            throw new JSException(new SyntaxError("Invalid string char '\\u000" + (int)value[i] + "'"));
                    }
                    if (stack.Peek().state == ParseState.Name)
                    {
                        stack.Peek().fieldName = value;
                        stack.Peek().state = ParseState.Value;
                        while (isSpace(code[pos]))
                            pos++;
                        if (code[pos] != ':')
                            throw new JSException((new SyntaxError("Unexpected token.")));
                        pos++;
                    }
                    else
                    {
                        value = Tools.Unescape(value, false);
                        if (stack.Peek().state != ParseState.Value)
                            throw new JSException((new SyntaxError("Unexpected token.")));
                        var v = stack.Peek();
                        v.state = ParseState.End;
                        v.value = value;
                    }
                }
                else if (Parser.Validate(code, "null", ref pos))
                {
                    if (stack.Peek().state != ParseState.Value)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    var v = stack.Peek();
                    v.state = ParseState.End;
                    v.value = JSObject.Null;
                }
                else if (Parser.Validate(code, "true", ref pos))
                {
                    if (stack.Peek().state != ParseState.Value)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    var v = stack.Peek();
                    v.state = ParseState.End;
                    v.value = true;
                }
                else if (Parser.Validate(code, "false", ref pos))
                {
                    if (stack.Peek().state != ParseState.Value)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    var v = stack.Peek();
                    v.state = ParseState.End;
                    v.value = false;
                }
                else if (code[pos] == '{')
                {
                    if (stack.Peek().state == ParseState.Name)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    stack.Peek().value = JSObject.CreateObject();
                    stack.Peek().state = ParseState.Object;
                    //stack.Push(new StackFrame() { state = ParseState.Name, container = stack.Peek().value });
                    pos++;
                }
                else if (code[pos] == '[')
                {
                    if (stack.Peek().state == ParseState.Name)
                        throw new JSException((new SyntaxError("Unexpected token.")));
                    stack.Peek().value = new Array();
                    stack.Peek().state = ParseState.Array;
                    //stack.Push(new StackFrame() { state = ParseState.Value, fieldName = (stack.Peek().valuesCount++).ToString(CultureInfo.InvariantCulture), container = stack.Peek().value });
                    pos++;
                }
                else if (stack.Peek().state != ParseState.End)
                    throw new JSException((new SyntaxError("Unexpected token.")));
                if (stack.Peek().state == ParseState.End)
                {
                    var t = stack.Pop();
                    if (reviewer != null)
                    {
                        revargs[0] = t.fieldName;
                        revargs[1] = t.value;
                        var val = reviewer.Invoke(revargs);
                        if (val.IsDefinded)
                        {
                            if (t.container != null)
                                t.container.GetMember(t.fieldName, true, true).Assign(val);
                            else
                            {
                                t.value = val;
                                stack.Push(t);
                            }
                        }
                    }
                    else if (t.container != null)
                        t.container.GetMember(t.fieldName, true, true).Assign(t.value);
                    else
                        stack.Push(t);
                }
                while (code.Length > pos && isSpace(code[pos]))
                    pos++;
                if (code.Length <= pos)
                {
                    if (stack.Peek().state != ParseState.End)
                        throw new JSException(new SyntaxError("Unexpected end of string."));
                    else
                        break;
                }
                switch (code[pos])
                {
                    case ',':
                        {
                            if (stack.Peek().state == ParseState.Array)
                                stack.Push(new StackFrame() { state = ParseState.Value, fieldName = (stack.Peek().valuesCount++).ToString(CultureInfo.InvariantCulture), container = stack.Peek().value });
                            else if (stack.Peek().state == ParseState.Object)
                                stack.Push(new StackFrame() { state = ParseState.Name, container = stack.Peek().value });
                            else
                                throw new JSException((new SyntaxError("Unexpected token.")));
                            pos++;
                            break;
                        }
                    case ']':
                        {
                            if (stack.Peek().state != ParseState.Array)
                                throw new JSException((new SyntaxError("Unexpected token.")));
                            stack.Peek().state = ParseState.End;
                            pos++;
                            break;
                        }
                    case '}':
                        {
                            if (stack.Peek().state != ParseState.Object)
                                throw new JSException((new SyntaxError("Unexpected token.")));
                            stack.Peek().state = ParseState.End;
                            pos++;
                            break;
                        }
                    default:
                        {
                            if (stack.Peek().state == ParseState.Array)
                                stack.Push(new StackFrame() { state = ParseState.Value, fieldName = (stack.Peek().valuesCount++).ToString(CultureInfo.InvariantCulture), container = stack.Peek().value });
                            else if (stack.Peek().state == ParseState.Object)
                                stack.Push(new StackFrame() { state = ParseState.Name, container = stack.Peek().value });
                            continue;
                        }
                }
                while (code.Length > pos && isSpace(code[pos]))
                    pos++;
                if (code.Length <= pos && stack.Peek().state != ParseState.End)
                    throw new JSException(new SyntaxError("Unexpected end of string."));
            }
            return stack.Pop().value;
        }

        [DoNotEnumerate]
        [ArgumentsLength(3)]
        public static JSObject stringify(Arguments args)
        {
            var length = args.length;
            Function replacer = length > 1 ? args[1].oValue as Function : null;
            string space = null;
            if (args.length > 2)
            {
                var sa = args[2];
                if (sa.valueType >= JSObjectType.Object
                    && sa.oValue != sa)
                    sa = sa.oValue as JSObject ?? sa;
                if (sa is ObjectContainer)
                    sa = (sa as ObjectContainer).Value as JSObject ?? sa;
                sa = sa.Value as JSObject ?? sa;
                if (sa.valueType == JSObjectType.Int
                    || sa.valueType == JSObjectType.Double
                    || sa.valueType == JSObjectType.String)
                {
                    if (sa.valueType == JSObjectType.Int)
                    {
                        if (sa.iValue > 0)
                            space = "          ".Substring(10 - System.Math.Max(0, System.Math.Min(10, sa.iValue)));
                    }
                    else if (sa.valueType == JSObjectType.Double)
                    {
                        if ((int)sa.dValue > 0)
                            space = "          ".Substring(10 - System.Math.Max(0, System.Math.Min(10, (int)sa.dValue)));
                    }
                    else
                    {
                        space = sa.ToString();
                        if (space.Length > 10)
                            space = space.Substring(0, 10);
                        if (space.Length == 0)
                            space = null;
                    }
                }
            }
            var target = args[0];
            return stringify(target.Value as JSObject ?? target, replacer, space) ?? JSObject.undefined;
        }

        [Hidden]
        public static string stringify(JSObject obj, Function replacer, string space)
        {
            return stringifyImpl("", obj, replacer, space, new List<JSObject>(), new Arguments());
        }

        private static void escapeIfNeed(StringBuilder sb, char c)
        {
            if ((c >= 0 && c <= 0x1f)
                || (c == '\\')
                || (c == '"'))
            {
                switch (c)
                {
                    case (char)8:
                        {
                            sb.Append("\\b");
                            break;
                        }
                    case (char)9:
                        {
                            sb.Append("\\t");
                            break;
                        }
                    case (char)10:
                        {
                            sb.Append("\\n");
                            break;
                        }
                    case (char)12:
                        {
                            sb.Append("\\f");
                            break;
                        }
                    case (char)13:
                        {
                            sb.Append("\\r");
                            break;
                        }
                    case '\\':
                        {
                            sb.Append("\\\\");
                            break;
                        }
                    case '"':
                        {
                            sb.Append("\\\"");
                            break;
                        }
                    default:
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                            break;
                        }
                }
            }
            else
                sb.Append(c);
        }

        private static string stringifyImpl(string key, JSObject obj, Function replacer, string space, List<JSObject> processed, Arguments args)
        {
            if (processed.IndexOf(obj) != -1)
                throw new JSException(new TypeError("Can not convert circular structure to JSON."));
            processed.Add(obj);
            try
            {
                {
                    if (replacer != null)
                    {
                        args[0] = "";
                        args[0].oValue = key;
                        args[1] = obj;
                        args.length = 2;
                        var t = replacer.Invoke(args);
                        if (t.valueType <= JSObjectType.Undefined || (t.valueType >= JSObjectType.Object && t.oValue == null))
                            return null;
                        obj = t;
                    }
                }
                if (obj.valueType <= JSObjectType.Undefined
                    || obj.valueType == JSObjectType.Function)
                    return null;
                obj = obj.Value as JSObject ?? obj;
                StringBuilder res = null;
                string strval = null;
                if (obj.valueType < JSObjectType.Object)
                {
                    if (obj.valueType == JSObjectType.String)
                    {
                        res = new StringBuilder("\"");
                        strval = obj.ToString();
                        for (var i = 0; i < strval.Length ; i++)
                            escapeIfNeed(res, strval[i]);
                        res.Append('"');
                        return res.ToString();
                    }
                    return obj.ToString();
                }
                if (obj.Value == null)
                    return "null";
                var toJSONmemb = obj["toJSON"];
                toJSONmemb = toJSONmemb.Value as JSObject ?? toJSONmemb;
                if (toJSONmemb.valueType == JSObjectType.Function)
                    return stringifyImpl("", (toJSONmemb.oValue as Function).Invoke(obj, null), null, space, processed, null);
                res = new StringBuilder(obj is Array ? "[" : "{");
                bool first = true;
                foreach (var member in obj)
                {
                    var value = obj[member];
                    value = value.Value as JSObject ?? value;
                    if (value.valueType < JSObjectType.Undefined)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = ((value.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(obj, null);
                    strval = stringifyImpl(member, value, replacer, space, processed, args);
                    if (strval == null)
                        continue;
                    if (!first)
                        res.Append(",");
                    if (space != null)
                        res.Append(Environment.NewLine);
                    if (space != null)
                        res.Append(space);
                    if (res[0] == '[')
                    {
                        if (space != null)
                            res.Append(space);
                        /*
                        for (var i = 0; i < strval.Length; i++)
                        {
                            escapeIfNeed(res, strval[i]);
                        }
                        */
                        res.Append(strval);
                    }
                    else
                    {
                        res.Append('"');
                        for (var i = 0; i < member.Length; i++)
                        {
                            escapeIfNeed(res, member[i]);
                        }
                        res.Append("\":")
                           .Append(space ?? "");
                        /*
                        if (strval.Length > 0 && strval[0] == '"')
                        {
                            res.Append(strval[0]);
                            for (var i = 1; i < strval.Length - 1; i++)
                            {
                                escapeIfNeed(res, strval[i]);
                            }
                            if (strval.Length > 1)
                                res.Append(strval[strval.Length - 1]);
                        }
                        else
                        */
                        {
                            for (var i = 0; i < strval.Length; i++)
                            {
                                res.Append(strval[i]);
                                if (i >= Environment.NewLine.Length && strval.IndexOf(Environment.NewLine, i - 1, Environment.NewLine.Length) != -1)
                                    res.Append(space);
                            }
                        }
                    }
                    first = false;
                }
                if (!first && space != null)
                    res.Append(Environment.NewLine);
                return res.Append(obj is Array ? "]" : "}").ToString();
            }
            finally
            {
                processed.RemoveAt(processed.Count - 1);
            }
        }
    }
}
