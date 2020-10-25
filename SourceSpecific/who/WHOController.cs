using CsvHelper;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace DataDownloader.who
{
    class WHO_Controller
    {
        string sourcefile;
        WHO_Processor processor;
        Source source;		
        string file_base;
        FileWriter file_writer;
        int saf_id;
        int source_id;
        LoggingDataLayer logging_repo;

        public WHO_Controller(string _sourcefile, int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
        {
            sourcefile = _sourcefile;
            processor = new WHO_Processor();
            source = _source;
            file_base = source.local_folder;
            source_id = source.id;
            saf_id = _saf_id;
            file_writer = new FileWriter(source);
            logging_repo = _logging_repo;
        }

        public DownloadResult ProcessFile()
        {
            // WHO processing unusual in that it is from a csv file
            // The program loops through the file and creates an XML file from each row
            // It then distributes it to the correct source folder for 
            // later harvesting.

            // In some cases the file will be one of a set created from a large
            // 'all data' download, in other cases it will be a weekly update file
            // In both cases any existing XML files of the same name 
            // shoud be overwritten

            // Although the args parameter is passed in for consistency it is not used.
            // The file may be a 'full' set, or more commonly a weekly update file, but this
            // does not affect the file's processing.

            XmlSerializer writer = new XmlSerializer(typeof(WHORecord));
            DownloadResult res = new DownloadResult();

            using (var reader = new StreamReader(sourcefile, true))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = false;
                    var records = csv.GetRecords<WHO_SourceRecord>();

                    // Consider each study in turn.

                    foreach (WHO_SourceRecord sr in records)
                    {
                        res.num_checked++;
                        WHORecord r = processor.ProcessStudyDetails(sr, logging_repo);

                        if (r != null)
                        {
                            // Write out study record as XML, log the download
                            string file_base = r.folder_name;
                            if (!Directory.Exists(file_base))
                            {
                                Directory.CreateDirectory(file_base);
                            }
                            string file_name = r.sd_sid + ".xml";
                            string full_path = Path.Combine(file_base, file_name);

                            file_writer.WriteWHOSourcedFile(writer, r, full_path);
                            bool added = logging_repo.UpdateStudyDownloadLog(r.source_id, r.sd_sid, r.remote_url, saf_id,
                                                               DateHelpers.FetchDateTimeFromISO(r.record_date), full_path);
                            res.num_downloaded++;
                            if (added) res.num_added++;
                        }
                        
                        if (res.num_checked % 100 ==0) StringHelpers.SendFeedback(res.num_checked.ToString());
                    }
                    
                    StringHelpers.SendFeedback("WHO file, number of records checked = " + res.num_checked.ToString());
                    StringHelpers.SendFeedback("WHO file, number of records downloaded = " + res.num_downloaded.ToString());
                    StringHelpers.SendFeedback("WHO file, number of records added = " + res.num_added.ToString());
                }
            }

            return res;
        }
    }
}
