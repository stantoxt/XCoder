using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using NewLife.Log;
using XCode.DataAccessLayer;
using XTemplate.Templating;
using NewLife.Reflection;
using NewLife.Collections;

namespace XCoder
{
    /// <summary>
    /// ������������
    /// </summary>
    public class Engine
    {
        #region ����
        public const String TemplatePath = "Template";

        private static Dictionary<String, String> _Templates;
        /// <summary>ģ��</summary>
        public static Dictionary<String, String> Templates
        {
            get
            {
                if (_Templates == null) _Templates = FileSource.GetTemplates();
                return _Templates;
            }
        }

        private static List<String> _FileTemplates;
        /// <summary>
        /// �ļ�ģ��
        /// </summary>
        public static List<String> FileTemplates
        {
            get
            {
                if (_FileTemplates == null)
                {
                    _FileTemplates = new List<string>();

                    String dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TemplatePath);
                    if (Directory.Exists(dir))
                    {
                        String[] ds = Directory.GetDirectories(dir);
                        if (ds != null && ds.Length > 0)
                        {
                            foreach (String item in ds)
                            {
                                DirectoryInfo di = new DirectoryInfo(item);
                                _FileTemplates.Add(di.Name);
                            }
                        }
                    }
                }
                return _FileTemplates;
            }
        }

        public Engine(XConfig config)
        {
            Config = config;
        }

        private XConfig _Config;
        /// <summary>����</summary>
        public XConfig Config
        {
            get { return _Config; }
            set { _Config = value; }
        }

        private DictionaryCache<String, List<IDataTable>> _cache = new DictionaryCache<String, List<IDataTable>>();

        private List<IDataTable> _Tables;
        /// <summary>���б�</summary>
        public List<IDataTable> Tables
        {
            get
            {
                // ��ͬ��ǰ׺����Сдѡ��õ��ı������ǲ�һ���ġ��������ֵ�������
                String key = String.Format("{0}_{1}_{2}", Config.AutoCutPrefix, Config.AutoFixWord, Config.Prefix);
                return _cache.GetItem(key, k => FixTable(_Tables));
                //return _Tables;
            }
            //set { _Tables = FixTable(value); }
            set { _Tables = value; }
        }

        /// <summary>���·��</summary>
        public String OuputPath
        {
            get
            {
                String str = Config.OutputPath;
                if (!Directory.Exists(str)) Directory.CreateDirectory(str);

                return str;
            }
        }
        #endregion

        #region ��������
        /// <summary>
        /// ����ǰ׺
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String CutPrefix(String name)
        {
            String oldname = name;

            if (String.IsNullOrEmpty(name)) return null;

            //�Զ�ȥ��ǰ׺
            if (XConfig.Current.AutoCutPrefix && name.Contains("_"))
            {
                name = name.Substring(name.IndexOf("_") + 1);
            }

            if (String.IsNullOrEmpty(XConfig.Current.Prefix))
            {
                if (IsKeyWord(name)) return oldname;
                return name;
            }
            String[] ss = XConfig.Current.Prefix.Split(new Char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String s in ss)
            {
                if (name.StartsWith(s))
                {
                    name = name.Substring(s.Length);
                }
                else if (name.EndsWith(s))
                {
                    name = name.Substring(0, name.Length - s.Length);
                }
            }

            if (IsKeyWord(name)) return oldname;

            return name;
        }

        private static CodeDomProvider[] _CGS;
        /// <summary>����������</summary>
        public static CodeDomProvider[] CGS
        {
            get
            {
                if (_CGS == null)
                {
                    _CGS = new CodeDomProvider[] { new CSharpCodeProvider(), new VBCodeProvider() };
                }
                return _CGS;
            }
        }

        /// <summary>
        /// ����Ƿ�Ϊc#�ؼ���
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static Boolean IsKeyWord(String name)
        {
            if (String.IsNullOrEmpty(name)) return false;

            foreach (CodeDomProvider item in CGS)
            {
                if (!item.IsValidIdentifier(name)) return true;
            }

            return false;
        }

        /// <summary>
        /// �Զ�������Сд
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String FixWord(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;

            if (name.Equals("ID", StringComparison.OrdinalIgnoreCase)) return "ID";

            if (name.Length <= 2) return name;

            Int32 count1 = 0;
            Int32 count2 = 0;
            foreach (Char item in name.ToCharArray())
            {
                if (item >= 'a' && item <= 'z')
                    count1++;
                else if (item >= 'A' && item <= 'Z')
                    count2++;
            }

            //û�л���ֻ��һ��Сд��ĸ�ģ���Ҫ����
            //û�д�д�ģ�ҲҪ����
            if (count1 <= 1 || count2 < 1)
            {
                name = name.ToLower();
                Char c = name[0];
                if (c >= 'a' && c <= 'z') c = (Char)(c - 'a' + 'A');
                name = c + name.Substring(1);
            }

            //����Is��ͷ�ģ���������ĸҪ��д
            if (name.StartsWith("Is") && name.Length >= 3)
            {
                Char c = name[2];
                if (c >= 'a' && c <= 'z')
                {
                    c = (Char)(c - 'a' + 'A');
                    name = name.Substring(0, 2) + c + name.Substring(3);
                }
            }

            //�Զ�ƥ�䵥��
            foreach (String item in Words.Keys)
            {
                if (name.Equals(item, StringComparison.OrdinalIgnoreCase))
                {
                    name = item;
                    break;
                }
            }

            return name;
        }

        /// <summary>
        /// Ӣ����ת������
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String ENameToCName(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;

            //foreach (String item in Words.Keys)
            //{
            //    if (name.Equals(item, StringComparison.OrdinalIgnoreCase)) return Words[item];
            //}
            //return null;
            String key = name.ToLower();
            if (LowerWords.ContainsKey(key))
                return LowerWords[key];
            else
                return null;
        }

        private static Dictionary<String, String> _Words;
        /// <summary>����</summary>
        public static Dictionary<String, String> Words
        {
            get
            {
                if (_Words == null)
                {
                    _Words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (File.Exists("e2c.txt"))
                    {
                        String content = File.ReadAllText("e2c.txt");
                        if (!String.IsNullOrEmpty(content))
                        {
                            String[] ss = content.Split(new Char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            if (ss != null && ss.Length > 0)
                            {
                                foreach (String item in ss)
                                {
                                    String[] s = item.Split(new Char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (s != null && s.Length > 0)
                                    {
                                        String str = "";
                                        if (s.Length > 1) str = s[1];
                                        if (!_Words.ContainsKey(s[0])) _Words.Add(s[0], str);
                                    }
                                }
                            }
                        }
                    }
                }
                return _Words;
            }
        }

        private static SortedList<String, String> _LowerWords;
        /// <summary>����</summary>
        public static SortedList<String, String> LowerWords
        {
            get
            {
                if (_LowerWords == null)
                {
                    _LowerWords = new SortedList<string, string>();

                    foreach (String item in Words.Keys)
                    {
                        if (!_LowerWords.ContainsKey(item.ToLower()))
                            _LowerWords.Add(item.ToLower(), Words[item]);
                        else if (String.IsNullOrEmpty(_LowerWords[item.ToLower()]))
                            _LowerWords[item.ToLower()] = Words[item];
                    }
                }
                return _LowerWords;
            }
        }

        public static void AddWord(String name, String cname)
        {
            String ename = CutPrefix(name);
            ename = FixWord(ename);
            if (LowerWords.ContainsKey(ename.ToLower())) return;
            LowerWords.Add(ename.ToLower(), cname);
            Words.Add(ename, cname);
            File.AppendAllText("e2c.txt", Environment.NewLine + ename + " " + cname, Encoding.UTF8);
        }
        #endregion

        #region ����
        /// <summary>
        /// ���ɴ��룬������Config����
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public String[] Render(String tableName)
        {
            List<IDataTable> tables = Tables;
            if (tables == null || tables.Count < 1) return null;

            IDataTable table = tables.Find(delegate(IDataTable item) { return String.Equals(item.Name, tableName, StringComparison.OrdinalIgnoreCase); });
            if (tableName == null) return null;

            Dictionary<String, Object> data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            data["Config"] = Config;
            data["Tables"] = tables;
            data["Table"] = table;

            // ����ģ������
            //Template tt = new Template();
            Template.Debug = Config.Debug;
            Dictionary<String, String> templates = new Dictionary<string, string>();
            String tempName = Config.TemplateName;
            if (tempName.StartsWith("*"))
            {
                // ϵͳģ��
                foreach (String item in Templates.Keys)
                {
                    String name = item.Substring(0, item.IndexOf("."));
                    if ("*" + name != tempName) continue;

                    String content = Templates[item];

                    // �����ļ�ͷ
                    if (Config.UseHeadTemplate && !String.IsNullOrEmpty(Config.HeadTemplate) && item.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        content = Config.HeadTemplate + content;

                    templates.Add(item.Substring(name.Length + 1), content);
                }
            }
            else
            {
                // �ļ�ģ��
                String dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TemplatePath);
                dir = Path.Combine(dir, tempName);
                String[] ss = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                if (ss != null && ss.Length > 0)
                {
                    foreach (String item in ss)
                    {
                        if (item.EndsWith("scc", StringComparison.OrdinalIgnoreCase)) continue;

                        String content = File.ReadAllText(item);

                        String name = item.Substring(dir.Length);
                        if (name.StartsWith(@"\")) name = name.Substring(1);

                        // �����ļ�ͷ
                        if (Config.UseHeadTemplate && !String.IsNullOrEmpty(Config.HeadTemplate) && name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                            content = Config.HeadTemplate + content;

                        templates.Add(name, content);
                    }
                }
            }
            if (templates.Count < 1) throw new Exception("û�п���ģ�棡");

            Template tt = Template.Create(templates);

            List<String> rs = new List<string>();
            foreach (TemplateItem item in tt.Templates)
            {
                if (item.Included) continue;

                String content = tt.Render(item.Name, data);

                // ��������ļ���
                String fileName = Path.GetFileName(item.Name);
                String className = CutPrefix(table.Name);
                className = FixWord(className);
                String remark = table.Description;
                if (String.IsNullOrEmpty(remark)) remark = ENameToCName(className);
                if (Config.UseCNFileName && !String.IsNullOrEmpty(remark)) className = remark;
                fileName = fileName.Replace("����", className).Replace("��˵��", remark).Replace("������", Config.EntityConnName);

                fileName = Path.Combine(OuputPath, fileName);

                String dir = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(fileName, content, Encoding.UTF8);

                rs.Add(content);
            }
            return rs.ToArray();
        }

        /// <summary>
        /// Ԥ�����������ȸ��ֶ�������ģ���д��
        /// ��Ϊ��������أ����ԣ�ÿ�θ������ú󣬶�Ӧ�õ���һ�θ÷�����
        /// </summary>
        List<IDataTable> FixTable(List<IDataTable> tables)
        {
            if (tables == null || tables.Count < 1) return tables;

            List<IDataTable> list = new List<IDataTable>();
            //foreach (IDataTable item in DAL.Create(Config.ConnName).Tables)
            //{
            //    list.Add(item.Clone() as IDataTable);
            //}
            foreach (IDataTable item in tables)
            {
                list.Add(item.Clone() as IDataTable);
            }
            //Tables = list;

            Dictionary<Object, String> noCNDic = new Dictionary<object, string>();

            #region ��������
            foreach (IDataTable table in list)
            {
                // ����������
                String name = table.Name;
                if (IsKeyWord(name)) name = name + "1";
                if (Config.AutoCutPrefix) name = CutPrefix(name);
                if (Config.AutoFixWord) name = FixWord(name);
                table.Alias = name;

                // ����
                if (Config.UseCNFileName)
                {
                    if (String.IsNullOrEmpty(table.Description)) table.Description = ENameToCName(table.Alias);

                    if (String.IsNullOrEmpty(table.Description)) noCNDic.Add(table, table.Alias);
                }

                // �ֶ�
                foreach (IDataColumn dc in table.Columns)
                {
                    name = dc.Name;
                    if (Config.AutoCutPrefix)
                    {
                        String s = CutPrefix(name);
                        if (dc.Table.Columns.Exists(item => item.Name == s)) name = s;
                        String str = table.Alias;
                        if (!s.Equals(str, StringComparison.OrdinalIgnoreCase) &&
                            s.StartsWith(str, StringComparison.OrdinalIgnoreCase) &&
                            s.Length > str.Length && Char.IsLetter(s, str.Length))
                            s = s.Substring(str.Length);
                        if (dc.Table.Columns.Exists(item => item.Name == s)) name = s;
                    }
                    if (Config.AutoFixWord)
                    {
                        name = FixWord(name);
                    }

                    dc.Alias = name;

                    // ����
                    if (Config.UseCNFileName)
                    {
                        if (String.IsNullOrEmpty(dc.Description)) dc.Description = Engine.ENameToCName(dc.Alias);

                        if (String.IsNullOrEmpty(dc.Description)) noCNDic.Add(dc, dc.Alias);
                    }
                }
            }
            #endregion

            #region �첽���ýӿ�����������
            if (Config.UseCNFileName && noCNDic.Count > 0)
            {
                ThreadPool.QueueUserWorkItem(TranslateWords, noCNDic);
            }
            #endregion

            return list;
        }

        void TranslateWords(Object state)
        {
            try
            {
                Dictionary<Object, String> dic = state as Dictionary<Object, String>;
                List<String> words = new List<string>();
                foreach (String item in dic.Values)
                {
                    if (Encoding.UTF8.GetByteCount(item) != item.Length) continue;

                    // �ִ�
                    String str = item;
                    List<String> ks = UpperCaseSplitWord(str);
                    str = String.Join(" ", ks.ToArray());

                    if (!String.IsNullOrEmpty(str) && !words.Contains(str)) words.Add(str);
                }

                ITranslate trs = new BingTranslate();
                String[] rs = trs.Translate(words.ToArray());
                if (rs == null || rs.Length < 1) return;

                Dictionary<String, String> ts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < words.Count && i < rs.Length; i++)
                {
                    String key = words[i].Replace(" ", null);
                    if (!ts.ContainsKey(key) && !String.IsNullOrEmpty(rs[i]) && words[i] != rs[i] && key != rs[i].Replace(" ", null)) ts.Add(key, rs[i].Replace(" ", null));
                }

                foreach (KeyValuePair<Object, String> item in dic)
                {
                    if (!ts.ContainsKey(item.Value) || String.IsNullOrEmpty(ts[item.Value])) continue;

                    if (item.Key is IDataTable)
                        (item.Key as IDataTable).Description = ts[item.Value];
                    else if (item.Key is IDataColumn)
                        (item.Key as IDataColumn).Description = ts[item.Value];
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.ToString());
            }
        }
        /// <summary>
        /// ��д��ĸ�ִ�
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static List<string> UpperCaseSplitWord(string s)
        {
            String str = s;
            List<string> ks = new List<string>();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                bool split = false;
                // �������Сд����Ϊ�߽磬����Ƿ���
                if (!(str[i] >= 'a' && str[i] <= 'z'))
                {
                    if (i > 0 && str[i - 1] >= 'a' && str[i - 1] <= 'z') // ǰһ���ַ���Сд
                    {
                        split = true;
                    }
                    else if (i + 1 < str.Length && str[i + 1] >= 'a' && str[i + 1] <= 'z') // ��һ���ַ���Сд
                    {
                        split = true;
                    }
                }
                if (split && sb.Replace(" ", "").Replace("_", "").Replace("-", "").Length > 0) // StringBuilder��Replace���޸����� �����������ֱ��ToString
                {
                    ks.Add(sb.ToString());
                    sb.Remove(0, sb.Length);
                }
                sb.Append(str[i]);
            }
            if (sb.Length > 0)
            {
                ks.Add(sb.ToString());
                sb.Remove(0, sb.Length);
            }
            return ks;
        }
        #endregion

        #region ��̬
        private static String _FileVersion;
        /// <summary>
        /// �ļ��汾
        /// </summary>
        public static String FileVersion
        {
            get
            {
                if (String.IsNullOrEmpty(_FileVersion))
                {
                    Assembly asm = Assembly.GetExecutingAssembly();
                    AssemblyFileVersionAttribute av = Attribute.GetCustomAttribute(asm, typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
                    if (av != null) _FileVersion = av.Version;
                    if (String.IsNullOrEmpty(_FileVersion)) _FileVersion = "1.0";
                }
                return _FileVersion;
            }
        }

        public static DateTime Compile
        {
            get
            {
                return AssemblyX.Create(Assembly.GetExecutingAssembly()).Compile;
            }
        }
        #endregion
    }
}