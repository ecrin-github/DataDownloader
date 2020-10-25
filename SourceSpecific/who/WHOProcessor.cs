using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DataDownloader.who
{
    public class WHO_Processor
    {
        public WHORecord ProcessStudyDetails(WHO_SourceRecord sr, LoggingDataLayer logging_repo)
        {
            WHORecord r = new WHORecord();

            string sd_sid = sr.TrialID.Replace("/", "-").Replace(@"\", "-").Replace(".", "-").Trim();
            r.sd_sid = sd_sid;
            int source_id = get_reg_source(sd_sid);

            if (source_id == 100120 || source_id == 100123 || source_id == 100126)
            { 
                // no need to process these - details input directly from registry
                // (for CGT, ISRCTN, EU CTR)
                return null; 
            }

            if (sd_sid == null || sd_sid == "null")
            {
                // Happens with one Dutch trial
                return null;
            }

            // otherwise proceed

            List<Secondary_Id> secondary_ids = new List<Secondary_Id>();
            List<StudyFeature> study_features = new List<StudyFeature>();
            r.country_list = new List<string>();

            r.source_id = source_id;
            r.record_date = DateHelpers.iso_date(sr.RecordDateString);

            SplitAndAddIds(secondary_ids, sd_sid, sr.SecondaryIDs, "secondary ids");

            r.public_title = StringHelpers.tidy_string(sr.public_title);
            r.scientific_title = StringHelpers.tidy_string(sr.Scientific_title);
            r.remote_url = StringHelpers.tidy_string(sr.url);

            r.public_contact_givenname = StringHelpers.tidy_string(sr.Public_Contact_Firstname);
            r.public_contact_familyname = StringHelpers.tidy_string(sr.Public_Contact_Lastname);
            r.public_contact_affiliation = StringHelpers.tidy_string(sr.Public_Contact_Affiliation);
            r.public_contact_email = StringHelpers.tidy_string(sr.Public_Contact_Email);
            r.scientific_contact_givenname = StringHelpers.tidy_string(sr.Scientific_Contact_Firstname);
            r.scientific_contact_familyname = StringHelpers.tidy_string(sr.Scientific_Contact_Lastname);
            r.scientific_contact_affiliation = StringHelpers.tidy_string(sr.Scientific_Contact_Affiliation);
            r.scientific_contact_email = StringHelpers.tidy_string(sr.Scientific_Contact_Email);

            string study_type = StringHelpers.tidy_string(sr.study_type);
            if (study_type != null)
            {
                string stype = study_type.ToLower();
                if (stype.StartsWith("intervention"))
                {
                    r.study_type = "Interventional";
                }
                else if (stype.StartsWith("observation")
                      || stype.StartsWith("epidem"))
                {
                    r.study_type = "Observational";
                }
                else
                {
                    r.study_type = "Other (" + r.study_type + ")";
                }
            }
            
            string study_status = StringHelpers.tidy_string(sr.Recruitment_status);
            if (study_status != null && study_status.Length > 5)
            {
                string status = study_status.ToLower();
                if (status == "complete"
                      || status == "completed"
                      || status == "complete: follow-up complete"
                      || status == "complete: follow up complete"
                      || status == "data analysis completed"
                      || status == "main results already published")
                {
                    r.study_status = "Completed";
                }
                else if (status == "complete: follow-up continuing"
                      || status == "complete: follow up continuing"
                      || status == "active, not recruiting"
                      || status == "closed to recruitment of participants"
                      || status == "no longer recruiting"
                      || status == "not recruiting"
                      || status == "recruitment completed")
                {
                    r.study_status = "Active, not recruiting";
                }
                else if (status == "recruiting"
                      || status == "open public recruiting"
                      || status == "open to recruitment")
                {
                    r.study_status = "Recruiting";
                }
                else if (status.Contains("pending")
                      || status == "not yet recruiting")
                {
                    r.study_status = "Not yet recruiting";
                }
                else if (status.Contains("suspended")
                      || status.Contains("temporarily closed"))
                {
                    r.study_status = "Suspended";
                }
                else if (status.Contains("terminated")
                      || status.Contains("stopped early"))
                {
                    r.study_status = "Terminated";
                }
                else if (status.Contains("withdrawn"))
                {
                    r.study_status = "Withdrawn";
                }
                else if (status.Contains("enrolling by invitation"))
                {
                    r.study_status = "Enrolling by invitation";
                }
                else 
                {
                    r.study_status = "Other (" + study_status + ")";
                }

            }

            r.date_registration = DateHelpers.iso_date(sr.Date_registration);
            r.date_enrolment = DateHelpers.iso_date(sr.Date_enrollement);

            r.target_size = StringHelpers.tidy_string(sr.Target_size);
            r.primary_sponsor = StringHelpers.tidy_string(sr.Primary_sponsor);
            r.secondary_sponsors = StringHelpers.tidy_string(sr.Secondary_sponsors);
            r.source_support = StringHelpers.tidy_string(sr.Source_Support);

            string design_list = StringHelpers.tidy_string(sr.study_design);
            if (design_list != null)
            {
                r.design_string = design_list;
                bool nonrandomised = false;
                string design = design_list.ToLower();
                if (design.Contains("non-randomized")
                 || design.Contains("nonrandomized")
                 || design.Contains("non-randomised")
                 || design.Contains("nonrandomised"))
                {
                    study_features.Add(new StudyFeature(22, "Allocation type", 210, "Nonrandomised"));
                    nonrandomised = true;
                }

                if (!nonrandomised && 
                     (  design.Contains("randomized")
                     || design.Contains("randomised")))
                {
                    study_features.Add(new StudyFeature(22, "Allocation type", 205, "Randomised"));
                }

                if (design.Contains("parallel"))
                {
                    study_features.Add(new StudyFeature(23, "Intervention model", 305, "Parallel assignment"));
                }

                if (design.Contains("crossover"))
                {
                    study_features.Add(new StudyFeature(23, "Intervention model", 310, "Crossover assignment"));
                }

                if (design.Contains("factorial"))
                {
                    study_features.Add(new StudyFeature(23, "Intervention model", 315, "Factorial assignment"));
                }

                if (r.study_type == "Observational")
                {
                    study_features.Add(new StudyFeature(24, "Masking", 599, "Not applicable"));
                }
                else
                {
                    if (design.Contains("open label")
                     || design.Contains("open-label")
                     || design.Contains("no mask")
                     || design.Contains("masking not used")
                     || design.Contains("not blinded")
                     || design.Contains("non-blinded")
                     || design.Contains("no blinding")
                     || design.Contains("no masking")
                     || design.Contains("masking: none")
                     || design.Contains("blinding: open")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 500, "None (Open Label)"));
                    }
                    else if (design.Contains("single blind")
                     || design.Contains("single-blind")
                     || design.Contains("single - blind")
                     || design.Contains("masking: single")
                     || design.Contains("outcome assessor blinded")
                     || design.Contains("participant blinded")
                     || design.Contains("investigator blinded")
                     || design.Contains("blinded (patient/subject)")
                     || design.Contains("blinded (investigator/therapist)")
                     || design.Contains("blinded (assessor)")
                     || design.Contains("blinded (data analyst)")
                     || design.Contains("uni-blind")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 505, "Single"));
                    }
                    else if (design.Contains("double blind")
                     || design.Contains("double-blind")
                     || design.Contains("doble-blind")
                     || design.Contains("double - blind")
                     || design.Contains("double-masked")
                     || design.Contains("masking: double")
                     || design.Contains("blinded (assessor, data analyst)")
                     || design.Contains("blinded (patient/subject, investigator/therapist")
                     || design.Contains("masking:participant, investigator, outcome assessor")
                     || design.Contains("participant and investigator blinded")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 510, "Double"));
                    }
                    else if (design.Contains("triple blind")
                     || design.Contains("triple-blind")
                     || design.Contains("blinded (patient/subject, caregiver, investigator/therapist, assessor")
                     || design.Contains("masking:participant, investigator, outcome assessor")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 515, "Triple"));
                    }
                    else if (design.Contains("quadruple blind")
                     || design.Contains("quadruple-blind")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 520, "Quadruple"));
                    }
                    else if (design.Contains("masking used")
                     || design.Contains("blinding used"))
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 502, "Blinded (no details)"));
                    }
                    else if (design.Contains("masking:not applicable")
                     || design.Contains("blinding:not applicable")
                     || design.Contains("masking not applicable")
                     || design.Contains("blinding not applicable")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 599, "Not applicable"));
                    }
                    else if (design.Contains("masking: unknown")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 525, "Not provided"));
                    }
                    else if (design.Contains("mask")
                     || design.Contains("blind")
                     )
                    {
                        study_features.Add(new StudyFeature(24, "Masking", 5000, design_list));
                    }
                }
            }

            string phase_list = StringHelpers.tidy_string(sr.phase);
            if (phase_list != null)
            {
                r.phase_string = phase_list;
                string phase = phase_list.ToLower();
                if (phase != "not selected"	&& phase != "not applicable" 
                    && phase != "na" && phase != "n/a")
                {
                    if (phase == "phase 0"
                     || phase == "phase-0"
                     || phase == "phase0"
                     || phase == "0"
                     || phase == "0 (exploratory trials)"
                     || phase == "phase 0 (exploratory trials)"
                     || phase == "0 (exploratory trials)")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 105, "Early phase 1"));
                    }
                    else if (phase == "1"
                          || phase == "i"
                          || phase == "i (phase i study)"
                          || phase == "phase-1"
                          || phase == "phase 1"
                          || phase == "phase i"
                          || phase == "phase1")
                    {
                        study_features.Add(new StudyFeature(20, "phase", 110, "Phase 1"));
                    }
                    else if (phase == "1-2"
                          || phase == "1 to 2"
                          || phase == "i-ii"
                          || phase == "i+ii (phase i+phase ii)"
                          || phase == "phase 1-2"
                          || phase == "phase 1 / phase 2"
                          || phase == "phase 1/ phase 2"
                          || phase == "phase 1/phase 2"
                          || phase == "phase i,ii"
                          || phase == "phase1/phase2")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 115, "Phase 1/Phase 2"));
                    }
                    else if (phase == "2"
                          || phase == "2a"
                          || phase == "2b"
                          || phase == "ii"
                          || phase == "ii (phase ii study)"
                          || phase == "iia"
                          || phase == "iib"
                          || phase == "phase-2"
                          || phase == "phase 2"
                          || phase == "phase ii"
                          || phase == "phase2")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 120, "Phase 2"));
                    }
                    else if (phase == "2-3"
                         || phase == "ii-iii"
                         || phase == "phase 2-3"
                         || phase == "phase 2 / phase 3"
                         || phase == "phase 2/ phase 3"
                         || phase == "phase 2/phase 3"
                         || phase == "phase2/phase3"
                         || phase == "phase ii,iii")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 125, "Phase 2/Phase 3"));
                    }
                    else if (phase == "3"
                          || phase == "iii"
                          || phase == "iii (phase iii study)"
                          || phase == "iiia"
                          || phase == "iiib"
                          || phase == "3-4"
                          || phase == "phase-3"
                          || phase == "phase 3"
                          || phase == "phase 3 / phase 4"
                          || phase == "phase 3/ phase 4"
                          || phase == "phase3"
                          || phase == "phase iii")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 130, "Phase 3"));
                    }
                    else if (phase == "4"
                           || phase == "iv"
                           || phase == "iv (phase iv study)"
                           || phase == "phase-4"
                           || phase == "phase 4"
                           || phase == "post-market"
                           || phase == "post marketing surveillance"
                           || phase == "phase4"
                           || phase == "phase iv")
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 135, "Phase 4"));
                    }
                    else
                    {
                        study_features.Add(new StudyFeature(20, "Phase", 1500, phase_list));
                    }
                }
            }

            r.country_list = WHOHelpers.split_and_dedup_string(sr.Countries);

            r.condition_list = GetConditions(sd_sid, sr.Conditions);

            r.interventions = StringHelpers.tidy_string(sr.Interventions);

            string agemin = StringHelpers.tidy_string(sr.Agemin);
            if(agemin != null)
            {
                if (Regex.Match(agemin, @"\d+").Success)
                {
                    r.agemin = Regex.Match(agemin, @"\d+").Value;
                    r.agemin_units = DateHelpers.GetTimeUnits(agemin);
                }
            }

            string agemax = StringHelpers.tidy_string(sr.Agemax);
            if (agemax != null)
            {
                if (Regex.Match(agemax, @"\d+").Success)
                {
                    r.agemax = Regex.Match(agemax, @"\d+").Value;
                    r.agemax_units = DateHelpers.GetTimeUnits(agemax);
                }
            }

            string gender = StringHelpers.tidy_string(sr.Gender);
            if (gender != null)
            {
                string gen = gender.ToLower();
                if (gen.Contains("both"))
                {
                    r.gender = "Both";
                }
                else
                {
                    bool M, F = false;
                    string gender_string = "";
                    F = gen.Contains("female") || gen.Contains("women") || gen == "f";
                    string gen2 = F ? gen.Replace("female", "").Replace("women", "") : gen;
                    M = gen2.Contains("male") || gen.Contains("men") || gen == "m" ;
                    
                    if (M && F)
                    {
                        gender_string = "Both";
                    }
                    else
                    {
                        if (M) gender_string = "Male";
                        if (F) gender_string = "Female";
                    }

                    if (gender == "-")
                    {
                        gender_string = "Not provided";
                    }

                    if (gender_string == "")
                    {
                        // still no match...
                        gender_string = "?? Unable to classify (" + gender + ")";
                    }
                    r.gender = gender_string;
                }
            }
            else
            {
                r.gender = "Not provided";
            }

            r.inclusion_criteria = StringHelpers.tidy_string(sr.Inclusion_Criteria);
            r.exclusion_criteria = StringHelpers.tidy_string(sr.Exclusion_Criteria);
            r.primary_outcome = StringHelpers.tidy_string(sr.Primary_Outcome);
            r.secondary_outcomes = StringHelpers.tidy_string(sr.Secondary_Outcomes);

            r.bridging_flag = StringHelpers.tidy_string(sr.Bridging_flag);
            if (r.bridging_flag != null && r.bridging_flag != r.sd_sid)
            {
                AddSecondaryId(secondary_ids, r.sd_sid, "bridging flag", r.bridging_flag);
            }

            r.bridged_type = StringHelpers.tidy_string(sr.Bridged_type);

            r.childs = StringHelpers.tidy_string(sr.Childs);
            SplitAndAddIds(secondary_ids, r.sd_sid, sr.Childs, "bridged child recs");

            r.type_enrolment = StringHelpers.tidy_string(sr.type_enrolment);
            r.retrospective_flag = StringHelpers.tidy_string(sr.Retrospective_flag);

            r.results_yes_no = StringHelpers.tidy_string(sr.results_yes_no);
            r.results_actual_enrollment = StringHelpers.tidy_string(sr.results_actual_enrollment);
            r.results_url_link = StringHelpers.tidy_string(sr.results_url_link);
            r.results_summary = StringHelpers.tidy_string(sr.results_summary);
            r.results_date_posted = DateHelpers.iso_date(sr.results_date_posted);
            r.results_date_first_publication = DateHelpers.iso_date(sr.results_date_first_publication);
            r.results_url_protocol = StringHelpers.tidy_string(sr.results_url_protocol);
            r.results_date_completed = DateHelpers.iso_date(sr.results_date_completed);

            string ipd_plan = StringHelpers.tidy_string(sr.results_IPD_plan);

            if (ipd_plan != null && ipd_plan.Length > 10)
            {
                if (ipd_plan.ToLower() != "not available" && ipd_plan.ToLower() != "not avavilable"
                && ipd_plan.ToLower() != "not applicable" && !ipd_plan.ToLower().StartsWith("justification or reason for"))
                {
                    r.ipd_plan = ipd_plan;
                }
            }

            string ipd_description = StringHelpers.tidy_string(sr.results_IPD_description);
            if (ipd_description != null && ipd_description.Length > 10)
            {
                if (ipd_description.ToLower() != "not available" && ipd_description.ToLower() != "not avavilable"
                && ipd_description.ToLower() != "not applicable" && !ipd_description.ToLower().StartsWith("justification or reason for"))
                {
                    r.ipd_description = ipd_description;
                }
            }

            r.folder_name = get_folder(source_id);
            r.secondary_ids = secondary_ids;
            r.study_features = study_features;

            return r;
        }


        private List<StudyCondition>  GetConditions(string sd_sid, string instring)
        {
            List<StudyCondition> conditions = new List<StudyCondition>();
            if (!string.IsNullOrEmpty(instring))
            {
                string condition_list = StringHelpers.tidy_string(instring);
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
                        char[] chars_to_lose = { ' ', '(', '.', '-', ';'}; 
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

                            char[] chars_to_lose2 = {' ', '-', ','};
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


        private void SplitAndAddIds(List<Secondary_Id> existing_ids, string sd_sid, 
                                             string instring, string source_field)
        {
            if (!string.IsNullOrEmpty(instring))
            {
                string id_list = StringHelpers.tidy_string(instring);
                if (!string.IsNullOrEmpty(id_list))
                {
                    List<string> ids = id_list.Split(";").ToList();
                    foreach (string s in ids)
                    {
                        char[] chars_to_lose = { ' ', '\'', '‘', '’', ';'};
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


        private void AddSecondaryId(List<Secondary_Id> existing_ids, string sd_sid, 
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

            else if(Regex.Match(sec_id, @"DRKS[0-9]{8}").Success)
            {
                processed_id = Regex.Match(sec_id, @"DRKS[0-9]{8}").Value;
                sec_id_source = 100124;
            }

            else if(Regex.Match(sec_id, @"CTRI/[0-9]{4}/[0-9]{2,3}/[0-9]{6}").Success)
            {
                processed_id = Regex.Match(sec_id, @"CTRI/[0-9]{4}/[0-9]{2,3}/[0-9]{6}").Value;
                processed_id = processed_id.Replace('/', '-');  // internal representation for CTRI
                sec_id_source = 100121;
            }

            else if(Regex.Match(sec_id, @"1111-[0-9]{4}-[0-9]{4}").Success)
            {
                processed_id = "U" + Regex.Match(sec_id, @"1111-[0-9]{4}-[0-9]{4}").Value;
                sec_id_source = 100115;
            }

            else if(Regex.Match(sec_id, @"UMIN[0-9]{9}").Success || Regex.Match(sec_id, @"UMIN-CTR[0-9]{9}").Success)
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


        private int get_reg_source(string trial_id)
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


        private string get_folder(int source_id)
        {
            string folder_path = "";
            switch (source_id)
            {
                case 100116: { folder_path = @"C:\Data\anzctr\"; break; }
                case 100117: { folder_path = @"C:\Data\rebec\"; break; }
                case 100118: { folder_path = @"C:\Data\chictr\"; break; }
                case 100119: { folder_path = @"C:\Data\cris\"; break; }
                case 100121: { folder_path = @"C:\Data\ctri\"; break; }
                case 100122: { folder_path = @"C:\Data\rpcec\"; break; }
                case 100124: { folder_path = @"C:\Data\drks\"; break; }
                case 100125: { folder_path = @"C:\Data\irct\"; break; }
                case 100127: { folder_path = @"C:\Data\jprn\"; break; }
                case 100128: { folder_path = @"C:\Data\pactr\"; break; }
                case 100129: { folder_path = @"C:\Data\rpuec\"; break; }
                case 100130: { folder_path = @"C:\Data\slctr\"; break; }
                case 100131: { folder_path = @"C:\Data\thctr\"; break; }
                case 100132: { folder_path = @"C:\Data\nntr\"; break; }
                case 101989: { folder_path = @"C:\Data\lebctr\"; break; }
            }
            return folder_path;
        }
        

    }
}



