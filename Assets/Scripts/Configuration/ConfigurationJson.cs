using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SiegeDemo
{
    // Strict schema/syntax gate around Unity's built-in serializer. The schema comes
    // directly from DemoContracts public fields, not a second set of DTOs.
    internal sealed class ConfigurationJson
    {
        private readonly string source;
        private int offset;
        private readonly Dictionary<string, HashSet<string>> levelFields = new Dictionary<string, HashSet<string>>();
        private int levelVersion;
        private ConfigurationJson(string source) { this.source = source; }

        public static void Check(string json)
        {
            Check(json, typeof(LevelData));
        }

        internal static void Check(string json, Type rootType)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("$: JSON 不能为空");
            var reader = new ConfigurationJson(json);
            if (json[0] == '\uFEFF') reader.offset++;
            reader.Value(rootType, "$", 0);
            reader.Space();
            if (reader.offset != json.Length) reader.Fail("$", "根对象后有多余内容");
            if (rootType == typeof(LevelData) && reader.levelVersion >= 2)
            {
                foreach (var fields in reader.levelFields)
                {
                    string[] required = fields.Key == "$" ? new[] { "id" } : new[] { "material", "isGround", "indestructible" };
                    foreach (string name in required) if (!fields.Value.Contains(name)) reader.Fail(fields.Key + "." + name, "版本 2 缺少必填字段");
                }
            }
        }

        private void Value(Type expected, string path, int depth)
        {
            if (depth > 32) Fail(path, "嵌套过深");
            Space();
            if (offset >= source.Length) Fail(path, "值不完整");
            if (source[offset] == 'n')
            {
                Literal("null", path);
                if (expected.IsValueType) Fail(path, "数值、向量和布尔字段不能为 null");
                return;
            }
            if (expected == typeof(string)) { ReadString(path); return; }
            if (expected == typeof(bool))
            {
                if (source[offset] == 't') Literal("true", path); else Literal("false", path);
                return;
            }
            if (expected == typeof(float) || expected == typeof(int) || expected.IsEnum)
            {
                string token = Number(path);
                if (expected == typeof(float))
                {
                    if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsInfinity(value) || float.IsNaN(value)) Fail(path, "数值超出有限 float 范围");
                }
                else
                {
                    if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int integer)) Fail(path, "必须是 int 范围内的整数");
                    else if (path == "$.version") levelVersion = integer;
                }
                return;
            }
            if (expected.IsArray)
            {
                Expect('[', path);
                Space();
                if (Take(']')) return;
                int index = 0;
                do { Value(expected.GetElementType(), path + "[" + index++ + "]", depth + 1); Space(); if (Take(']')) return; Expect(',', path); } while (true);
            }
            Expect('{', path);
            var members = expected.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Space();
            if (!Take('}')) do
            {
                Space();
                string key = ReadString(path);
                string fieldPath = path + "." + key;
                if (!seen.Add(key)) Fail(fieldPath, "JSON 字段重复");
                FieldInfo member = Array.Find(members, field => field.Name == key);
                if (member == null) Fail(fieldPath, "未知字段，请检查拼写");
                Space();
                Expect(':', fieldPath);
                Value(member.FieldType, fieldPath, depth + 1);
                Space();
                if (Take('}')) break;
                Expect(',', path);
            } while (true);
            foreach (var member in members)
            {
                // Top-level optional lists can be omitted. All scalar settings and
                // placement vectors are explicit to avoid JsonUtility default ambiguity.
                bool optional = expected == typeof(LevelData) && member.FieldType.IsArray && member.Name != "parts";
                optional |= expected == typeof(LevelData) && member.Name == "id";
                optional |= expected == typeof(PartDefinition) && (member.Name == "material" || member.Name == "isGround" || member.Name == "indestructible");
                if (!optional && !seen.Contains(member.Name)) Fail(path + "." + member.Name, "缺少必填字段");
            }
            if (expected == typeof(LevelData) || expected == typeof(PartDefinition)) levelFields[path] = seen;
        }

        private string Number(string path)
        {
            int start = offset;
            Take('-');
            if (!Take('0')) Digits(path);
            if (Take('.')) Digits(path);
            if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); Digits(path); }
            return source.Substring(start, offset - start);
        }
        private void Digits(string path)
        {
            int start = offset;
            while (offset < source.Length && source[offset] >= '0' && source[offset] <= '9') offset++;
            if (start == offset) Fail(path, "无效 JSON 数字");
        }
        private string ReadString(string path)
        {
            Expect('"', path);
            var result = new StringBuilder();
            while (offset < source.Length)
            {
                char value = source[offset++];
                if (value == '"') return result.ToString();
                if (value < 32) Fail(path, "字符串中不能包含未转义控制字符");
                if (value != '\\') { result.Append(value); continue; }
                if (offset == source.Length) Fail(path, "字符串转义不完整");
                char escape = source[offset++];
                switch (escape)
                {
                    case '"': case '\\': case '/': result.Append(escape); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (offset + 4 > source.Length || !ushort.TryParse(source.Substring(offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort unicode)) Fail(path, "无效 Unicode 转义");
                        else { result.Append((char)unicode); offset += 4; }
                        break;
                    default: Fail(path, "无效字符串转义"); break;
                }
            }
            Fail(path, "字符串未闭合");
            return null;
        }
        private void Literal(string value, string path)
        {
            if (offset + value.Length > source.Length || string.CompareOrdinal(source, offset, value, 0, value.Length) != 0) Fail(path, "预期 " + value);
            offset += value.Length;
        }
        private bool Take(char value)
        {
            if (offset < source.Length && source[offset] == value) { offset++; return true; }
            return false;
        }
        private void Expect(char value, string path) { if (!Take(value)) Fail(path, "预期 '" + value + "'"); }
        private void Space()
        {
            while (offset < source.Length && (source[offset] == ' ' || source[offset] == '\r' || source[offset] == '\n' || source[offset] == '\t')) offset++;
        }
        private void Fail(string path, string message) { throw new FormatException(path + " (字符 " + offset + "): " + message); }
    }
}
