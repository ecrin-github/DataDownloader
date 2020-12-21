using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DataDownloader.who
{
    public class WHOHelpers
    {
        LoggingDataLayer logging_repo;
        StringHelpers sh;

        public WHOHelpers(LoggingDataLayer _logging_repo)
        {
            logging_repo = _logging_repo;
            sh = new StringHelpers(_logging_repo);
        }

        public List<string> split_string(string instring)
        {
            if (string.IsNullOrEmpty(instring))
            {
                return null;
            }
            else
            {
                string string_list = sh.tidy_string(instring);
                if (string.IsNullOrEmpty(string_list))
                {
                    return null;
                }
                else
                {
                    return string_list.Split(";").ToList();
                }
            }
        }


        public List<string> split_and_dedup_string(string instring)
        {

            if (string.IsNullOrEmpty(instring))
            {
                return null;
            }
            else
            {
                string id_list = sh.tidy_string(instring);
                if (string.IsNullOrEmpty(id_list))
                {
                    return null;
                }
                else
                {
                    List<string> outstrings = new List<string>();
                    List<string> instrings = id_list.Split(";").ToList();
                    foreach (string s in instrings)
                    {
                        if (outstrings.Count == 0)
                        {
                            outstrings.Add(s);
                        }
                        else
                        {
                            bool add_string = true;
                            foreach (string s2 in outstrings)
                            {
                                if (s2 == s)
                                {
                                    add_string = false;
                                    break;
                                }
                            }
                            if (add_string) outstrings.Add(s);
                        }
                    }
                    return outstrings;
                }
            }

        }


        public List<StudyCondition> GetConditions(string sd_sid, string instring)
        {
            List<StudyCondition> conditions = new List<StudyCondition>();
            if (!string.IsNullOrEmpty(instring))
            {
                string condition_list = sh.tidy_string(instring);
                if (!string.IsNullOrEmpty(condition_list))
                {
                    // replace escaped characters to remove the semi-colons
                    string rsq = "’";
                    condition_list = condition_list.Replace("&lt;", "<").Replace("&gt;", ">");
                    condition_list = condition_list.Replace("&#39;", rsq).Replace("&rsquo;", rsq);

                    // replace line breaks and hashes with semi-colons, and split
                    condition_list = condition_list.Replace("<br>", ";").Replace("<br/>", ";");
                    condition_list = condition_list.Replace("#", ";");
                    List<string> conds = condition_list.Split(";").ToList();
                    foreach (string s in conds)
                    {
                        char[] chars_to_lose = { ' ', '(', '.', '-', ';' };
                        string s1 = s.Trim(chars_to_lose);
                        if (s1 != "" && s1.Length > 4)
                        {
                            // does it have an ICD code or similar at the front?
                            // if so extract and put code in code field

                            // Need a regex here to pick up ICD codes
                            string code = "", code_system = "";

                            if (s1.Contains("generalization"))
                            {
                                string code_string = "";
                                if (Regex.Match(s1, @"^[A-Z]\d{2}.\d{2} - \[generalization [A-Z]\d{2}.\d:").Success)
                                {
                                    code_string = Regex.Match(s1, @"^[A-Z]\d{2}.\d{2} - \[generalization [A-Z]\d{2}.\d:").Value.Trim();
                                    code = Regex.Match(code_string, @"[A-Z]\d{2}.\d:$").Value.Trim(':');
                                }

                                else if (Regex.Match(s1, @"^[A-Z]\d{2}.\d{2} - \[generalization [A-Z]\d{2}:").Success)
                                {
                                    code_string = Regex.Match(s1, @"^[A-Z]\d{2}.\d{2} - \[generalization [A-Z]\d{2}:").Value.Trim();
                                    code = Regex.Match(code_string, @"[A-Z]\d{2}.\d:$").Value.Trim(':');
                                }

                                else if (Regex.Match(s1, @"^[A-Z]\d{2}.\d - \[generalization [A-Z]\d{2}").Success)
                                {
                                    code_string = Regex.Match(s1, @"^[A-Z]\d{2}.\d - \[generalization [A-Z]\d{2}:").Value.Trim();
                                    code = Regex.Match(code_string, @"[A-Z]\d{2}:$").Value.Trim(':');
                                }

                                code_system = "ICD 10";
                                s1 = s1.Substring(code_string.Length).Trim(']').Trim();
                            }

                            else if (Regex.Match(s1, @"^[A-Z]\d{2}(.\d)? ").Success)
                            {
                                code = Regex.Match(s1, @"^[A-Z]\d{2}(.\d)? ").Value.Trim();
                                code_system = "ICD 10";
                                s1 = s1.Substring(code.Length).Trim();
                            }

                            else if (Regex.Match(s1, @"^[A-Z]\d{2}-[A-Z]\d{2} ").Success)
                            {
                                code = Regex.Match(s1, @"^[A-Z]\d{2}-[A-Z]\d{2} ").Value.Trim();
                                code_system = "ICD 10";
                                s1 = s1.Substring(code.Length).Trim();
                            }

                            else if (Regex.Match(s1, @"^[A-Z]\d{2} - [A-Z]\d{2} ").Success)
                            {
                                code = Regex.Match(s1, @"^[A-Z]\d{2} - [A-Z]\d{2} ").Value.Trim();
                                code_system = "ICD 10";
                                s1 = s1.Substring(code.Length).Trim();
                            }

                            else if (Regex.Match(s1, @"^[A-Z]\d{3} ").Success)
                            {
                                code = Regex.Match(s1, @"^[A-Z]\d{3} ").Value.Trim();
                                code_system = "ICD 10";
                                s1 = s1.Substring(code.Length).Trim();
                            }

                            char[] chars_to_lose2 = { ' ', '-', ',' };
                            s1 = s1.Trim(chars_to_lose2);

                            // check not duplicated
                            bool add_condition = true;
                            if (conditions.Count > 0)
                            {
                                foreach (StudyCondition sc in conditions)
                                {
                                    if (s1.ToLower() == sc.condition.ToLower())
                                    {
                                        add_condition = false;
                                        break;
                                    }
                                }
                            }

                            // check not a too broad ICD10 classification
                            if (Regex.Match(s1, @"^[A-Z]\d{2}-[A-Z]\d{2}$").Success)
                            {
                                add_condition = false;
                            }

                            if (add_condition)
                            {
                                if (code == "")
                                {
                                    conditions.Add(new StudyCondition(s1));
                                }
                                else
                                {
                                    conditions.Add(new StudyCondition(s1, code, code_system));

                                }
                            }
                        }
                    }
                }
            }
            return conditions;
        }


        public void SplitAndAddIds(List<Secondary_Id> existing_ids, string sd_sid,
                                             string instring, string source_field)
        {
            if (!string.IsNullOrEmpty(instring))
            {
                string id_list = sh.tidy_string(instring);
                if (!string.IsNullOrEmpty(id_list))
                {
                    List<string> ids = id_list.Split(";").ToList();
                    foreach (string s in ids)
                    {
                        char[] chars_to_lose = { ' ', '\'', '‘', '’', ';' };
                        string s1 = s.Trim(chars_to_lose);
                        if (s1.Length >= 4 && s1 != sd_sid)
                        {
                            string s2 = s1.ToLower();
                            if (Regex.Match(s2, @"\d").Success   // has to include at least 1 number
                                && !(s2.StartsWith("none"))
                                && !(s2.StartsWith("nil"))
                                && !(s2.StartsWith("not "))
                                && !(s2.StartsWith("date"))
                                && !(s2.StartsWith("version"))
                                && !(s2.StartsWith("??")))
                            {
                                AddSecondaryId(existing_ids, sd_sid, source_field, s1);
                            }
                        }
                    }
                }
            }
        }


        public void AddSecondaryId(List<Secondary_Id> existing_ids, string sd_sid,
                                     string source_field, string sec_id)
        {

            string interim_id = "", processed_id = null;
            int? sec_id_source = null;
            if (sec_id.Contains("NCT"))
            {
                interim_id = sec_id.Replace("NCT ", "NCT");
                interim_id = interim_id.Replace("NCTNumber", "");
                if (Regex.Match(interim_id, @"NCT[0-9]{8}").Success)
                {
                    processed_id = Regex.Match(interim_id, @"NCT[0-9]{8}").Value;
                    sec_id_source = 100120;
                }
                if (processed_id == "NCT11111111" || processed_id == "NCT99999999"
                    || processed_id == "NCT12345678" || processed_id == "NCT87654321")
                {
                    // remove these 
                    processed_id = null;
                    sec_id_source = null;
                }
            }

            else if (Regex.Match(sec_id, @"[0-9]{4}-[0-9]{6}-[0-9]{2}").Success)
            {
                processed_id = Regex.Match(sec_id, @"[0-9]{4}-[0-9]{6}-[0-9]{2}").Value;
                sec_id_source = 100123;

                if (processed_id == "--------------")
                {
                    // remove these 
                    processed_id = null;
                    sec_id_source = null;
                }
            }

            else if (sec_id.Contains("ISRCTN"))
            {
                interim_id = interim_id.Replace("(ISRCTN)", "");
                interim_id = interim_id.Replace("ISRCTN(International", "");
                interim_id = sec_id.Replace("ISRCTN ", "ISRCTN");
                interim_id = interim_id.Replace("ISRCTN: ", "ISRCTN");
                interim_id = interim_id.Replace("ISRCTNISRCTN", "ISRCTN");

                if (Regex.Match(interim_id, @"ISRCTN[0-9]{8}").Success)
                {
                    processed_id = Regex.Match(interim_id, @"ISRCTN[0-9]{8}").Value;
                    sec_id_source = 100126;
                }
            }

            else if (Regex.Match(sec_id, @"ACTRN[0-9]{14}").Success)
            {
                processed_id = Regex.Match(sec_id, @"ACTRN[0-9]{14}").Value;
                sec_id_source = 100116;
            }

            else if (Regex.Match(sec_id, @"DRKS[0-9]{8}").Success)
            {
                processed_id = Regex.Match(sec_id, @"DRKS[0-9]{8}").Value;
                sec_id_source = 100124;
            }

            else if (Regex.Match(sec_id, @"CTRI/[0-9]{4}/[0-9]{2,3}/[0-9]{6}").Success)
            {
                processed_id = Regex.Match(sec_id, @"CTRI/[0-9]{4}/[0-9]{2,3}/[0-9]{6}").Value;
                processed_id = processed_id.Replace('/', '-');  // internal representation for CTRI
                sec_id_source = 100121;
            }

            else if (Regex.Match(sec_id, @"1111-[0-9]{4}-[0-9]{4}").Success)
            {
                processed_id = "U" + Regex.Match(sec_id, @"1111-[0-9]{4}-[0-9]{4}").Value;
                sec_id_source = 100115;
            }

            else if (Regex.Match(sec_id, @"UMIN[0-9]{9}").Success || Regex.Match(sec_id, @"UMIN-CTR[0-9]{9}").Success)
            {
                processed_id = "JPRN-UMIN" + Regex.Match(sec_id, @"[0-9]{9}").Value;
                sec_id_source = 100127;
            }

            else if (Regex.Match(sec_id, @"jRCTs[0-9]{9}").Success)
            {
                processed_id = "JPRN-jRCTs" + Regex.Match(sec_id, @"[0-9]{9}").Value;
                sec_id_source = 100127;
            }

            else if (Regex.Match(sec_id, @"jRCT[0-9]{10}").Success)
            {
                processed_id = "JPRN-jRCT" + Regex.Match(sec_id, @"[0-9]{10}").Value;
                sec_id_source = 100127;
            }

            else if (sec_id.StartsWith("JPRN"))
            {
                if (Regex.Match(sec_id, @"^[0-9]{8}$").Success)
                {
                    processed_id = "JPRN-UMIN" + Regex.Match(sec_id, @"[0-9]{8}").Value;
                    sec_id_source = 100127;
                }
                else
                {
                    processed_id = sec_id;
                    sec_id_source = 100127;
                }
            }
            else if (sec_id.StartsWith("RBR"))
            {
                sec_id_source = 100117;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("ChiCTR"))
            {
                sec_id_source = 100118;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("KCT"))
            {
                sec_id_source = 100119;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("RPCEC"))
            {
                sec_id_source = 100122;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("DRKS"))
            {
                sec_id_source = 100124;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("IRCT"))
            {
                sec_id_source = 100125;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("PACTR"))
            {
                sec_id_source = 100128;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("PER"))
            {
                sec_id_source = 100129;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("SLCTR"))
            {
                sec_id_source = 100130;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("TCTR"))
            {
                sec_id_source = 100131;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("NL") || sec_id.StartsWith("NTR"))
            {
                sec_id_source = 100132;
                processed_id = sec_id;
            }
            else if (sec_id.StartsWith("LBCTR"))
            {
                sec_id_source = 101989;
                processed_id = sec_id;
            }


            if (sd_sid.StartsWith("RBR"))
            {
                // Extract Brazilian ethics Ids
                if (Regex.Match(sec_id, @"[0-9]{8}.[0-9].[0-9]{4}.[0-9]{4}").Success)
                {
                    processed_id = Regex.Match(sec_id, @"[0-9]{8}.[0-9].[0-9]{4}.[0-9]{4}").Value;
                    sec_id_source = 102000;  // Brasilian regulatory authority, ANVISA
                    // number is an ethics approval submission id
                }

                if (Regex.Match(sec_id, @"[0-9].[0-9]{3}.[0-9]{3}").Success)
                {
                    processed_id = Regex.Match(sec_id, @"[0-9].[0-9]{3}.[0-9]{3}").Value;
                    sec_id_source = 102001;  // Brasilian ethics committee approval number
                }
            }

            if (processed_id == null)
            {
                processed_id = sec_id;
            }

            // has this id been added before?
            bool add_id = true;
            if (existing_ids.Count > 0)
            {
                foreach (Secondary_Id s in existing_ids)
                {
                    if (processed_id == s.processed_id)
                    {
                        add_id = false;
                        break;
                    }
                }
            }
            if (add_id)
            {
                Secondary_Id secid = new Secondary_Id(source_field, sec_id, processed_id, sec_id_source);
                existing_ids.Add(secid);
            }
        }


        public int get_reg_source(string trial_id)
        {
            int source_id = 0;
            string tid = trial_id.ToUpper();
            if (tid.StartsWith("NCT"))
            {
                source_id = 100120;
            }
            else if (tid.StartsWith("EUCTR"))
            {
                source_id = 100123;
            }
            else if (tid.StartsWith("JPRN"))
            {
                source_id = 100127;
            }
            else if (tid.StartsWith("ACTRN"))
            {
                source_id = 100116;
            }
            else if (tid.StartsWith("RBR"))
            {
                source_id = 100117;
            }
            else if (tid.StartsWith("CHICTR"))
            {
                source_id = 100118;
            }
            else if (tid.StartsWith("KCT"))
            {
                source_id = 100119;
            }
            else if (tid.StartsWith("CTRI"))
            {
                source_id = 100121;
            }
            else if (tid.StartsWith("RPCEC"))
            {
                source_id = 100122;
            }
            else if (tid.StartsWith("DRKS"))
            {
                source_id = 100124;
            }
            else if (tid.StartsWith("IRCT"))
            {
                source_id = 100125;
            }
            else if (tid.StartsWith("ISRCTN"))
            {
                source_id = 100126;
            }
            else if (tid.StartsWith("PACTR"))
            {
                source_id = 100128;
            }
            else if (tid.StartsWith("PER"))
            {
                source_id = 100129;
            }
            else if (tid.StartsWith("SLCTR"))
            {
                source_id = 100130;
            }
            else if (tid.StartsWith("TCTR"))
            {
                source_id = 100131;
            }
            else if (tid.StartsWith("NL") || tid.StartsWith("NTR"))
            {
                source_id = 100132;
            }
            else if (tid.StartsWith("LBCTR"))
            {
                source_id = 101989;
            }
            else
            {
                source_id = 0;
            }
            return source_id;
        }


        public string get_folder(int source_id)
        {
            string folder_path = "";
            switch (source_id)
            {
                case 100116: { folder_path = @"C:\MDR_Data\anzctr\"; break; }
                case 100117: { folder_path = @"C:\MDR_Data\rebec\"; break; }
                case 100118: { folder_path = @"C:\MDR_Data\chictr\"; break; }
                case 100119: { folder_path = @"C:\MDR_Data\cris\"; break; }
                case 100121: { folder_path = @"C:\MDR_Data\ctri\"; break; }
                case 100122: { folder_path = @"C:\MDR_Data\rpcec\"; break; }
                case 100124: { folder_path = @"C:\MDR_Data\drks\"; break; }
                case 100125: { folder_path = @"C:\MDR_Data\irct\"; break; }
                case 100127: { folder_path = @"C:\MDR_Data\jprn\"; break; }
                case 100128: { folder_path = @"C:\MDR_Data\pactr\"; break; }
                case 100129: { folder_path = @"C:\MDR_Data\rpuec\"; break; }
                case 100130: { folder_path = @"C:\MDR_Data\slctr\"; break; }
                case 100131: { folder_path = @"C:\MDR_Data\thctr\"; break; }
                case 100132: { folder_path = @"C:\MDR_Data\nntr\"; break; }
                case 101989: { folder_path = @"C:\MDR_Data\lebctr\"; break; }
            }
            return folder_path;
        }
    }
}
