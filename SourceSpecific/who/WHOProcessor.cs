using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DataDownloader.who
{
    public class WHO_Processor
    {
        public WHORecord ProcessStudyDetails(WHO_SourceRecord sr, LoggingDataLayer logging_repo)
        {
            DateHelpers dh = new DateHelpers(logging_repo);
            StringHelpers sh = new StringHelpers(logging_repo);
            WHOHelpers wh = new WHOHelpers(logging_repo);

            WHORecord r = new WHORecord();

            string sd_sid = sr.TrialID.Replace("/", "-").Replace(@"\", "-").Replace(".", "-").Trim();
            r.sd_sid = sd_sid;
            int source_id = wh.get_reg_source(sd_sid);

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
            r.record_date = dh.iso_date(sr.last_updated);

            wh.SplitAndAddIds(secondary_ids, sd_sid, sr.SecondaryIDs, "secondary ids");

            r.public_title = sh.tidy_string(sr.public_title);
            r.scientific_title = sh.tidy_string(sr.Scientific_title);
            r.remote_url = sh.tidy_string(sr.url);

            r.public_contact_givenname = sh.tidy_string(sr.Public_Contact_Firstname);
            r.public_contact_familyname = sh.tidy_string(sr.Public_Contact_Lastname);
            r.public_contact_affiliation = sh.tidy_string(sr.Public_Contact_Affiliation);
            r.public_contact_email = sh.tidy_string(sr.Public_Contact_Email);
            r.scientific_contact_givenname = sh.tidy_string(sr.Scientific_Contact_Firstname);
            r.scientific_contact_familyname = sh.tidy_string(sr.Scientific_Contact_Lastname);
            r.scientific_contact_affiliation = sh.tidy_string(sr.Scientific_Contact_Affiliation);
            r.scientific_contact_email = sh.tidy_string(sr.Scientific_Contact_Email);

            string study_type = sh.tidy_string(sr.study_type);
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
            
            string study_status = sh.tidy_string(sr.Recruitment_status);
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

            r.date_registration = dh.iso_date(sr.Date_registration);
            r.date_enrolment = dh.iso_date(sr.Date_enrollement);

            r.target_size = sh.tidy_string(sr.Target_size);
            r.primary_sponsor = sh.tidy_string(sr.Primary_sponsor);
            r.secondary_sponsors = sh.tidy_string(sr.Secondary_sponsors);
            r.source_support = sh.tidy_string(sr.Source_Support);

            string design_list = sh.tidy_string(sr.study_design);
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

            string phase_list = sh.tidy_string(sr.phase);
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

            r.country_list = wh.split_and_dedup_string(sr.Countries);

            r.condition_list = wh.GetConditions(sd_sid, sr.Conditions);

            r.interventions = sh.tidy_string(sr.Interventions);

            string agemin = sh.tidy_string(sr.Age_min);
            if(agemin != null)
            {
                if (Regex.Match(agemin, @"\d+").Success)
                {
                    r.agemin = Regex.Match(agemin, @"\d+").Value;
                    r.agemin_units = dh.GetTimeUnits(agemin);
                }
            }

            string agemax = sh.tidy_string(sr.Age_max);
            if (agemax != null)
            {
                if (Regex.Match(agemax, @"\d+").Success)
                {
                    r.agemax = Regex.Match(agemax, @"\d+").Value;
                    r.agemax_units = dh.GetTimeUnits(agemax);
                }
            }

            string gender = sh.tidy_string(sr.Gender);
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

            r.inclusion_criteria = sh.tidy_string(sr.Inclusion_Criteria);
            r.exclusion_criteria = sh.tidy_string(sr.Exclusion_Criteria);
            r.primary_outcome = sh.tidy_string(sr.Primary_Outcome);
            r.secondary_outcomes = sh.tidy_string(sr.Secondary_Outcomes);

            r.bridging_flag = sh.tidy_string(sr.Bridging_flag);
            if (r.bridging_flag != null && r.bridging_flag != r.sd_sid)
            {
                wh.AddSecondaryId(secondary_ids, r.sd_sid, "bridging flag", r.bridging_flag);
            }

            r.bridged_type = sh.tidy_string(sr.Bridged_type);

            r.childs = sh.tidy_string(sr.Childs);
            wh.SplitAndAddIds(secondary_ids, r.sd_sid, sr.Childs, "bridged child recs");

            r.type_enrolment = sh.tidy_string(sr.type_enrolment);
            r.retrospective_flag = sh.tidy_string(sr.Retrospective_flag);

            r.results_yes_no = sh.tidy_string(sr.results_yes_no);
            r.results_actual_enrollment = sh.tidy_string(sr.results_actual_enrollment);
            r.results_url_link = sh.tidy_string(sr.results_url_link);
            r.results_summary = sh.tidy_string(sr.results_summary);
            r.results_date_posted = dh.iso_date(sr.results_date_posted);
            r.results_date_first_publication = dh.iso_date(sr.results_date_first_publication);
            r.results_url_protocol = sh.tidy_string(sr.results_url_protocol);
            r.results_date_completed = dh.iso_date(sr.results_date_completed);

            string ipd_plan = sh.tidy_string(sr.results_IPD_plan);

            if (ipd_plan != null && ipd_plan.Length > 10)
            {
                if (ipd_plan.ToLower() != "not available" && ipd_plan.ToLower() != "not avavilable"
                && ipd_plan.ToLower() != "not applicable" && !ipd_plan.ToLower().StartsWith("justification or reason for"))
                {
                    r.ipd_plan = ipd_plan;
                }
            }

            string ipd_description = sh.tidy_string(sr.results_IPD_description);
            if (ipd_description != null && ipd_description.Length > 10)
            {
                if (ipd_description.ToLower() != "not available" && ipd_description.ToLower() != "not avavilable"
                && ipd_description.ToLower() != "not applicable" && !ipd_description.ToLower().StartsWith("justification or reason for"))
                {
                    r.ipd_description = ipd_description;
                }
            }

            r.folder_name = wh.get_folder(source_id);
            r.secondary_ids = secondary_ids;
            r.study_features = study_features;

            return r;
        }
    }
}



