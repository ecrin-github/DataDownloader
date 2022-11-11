using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace DataDownloader.who
{
    class WHO_Controller
    {
        string _sourcefile;
        WHO_Processor _processor;
        Source _source;		
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        int _source_id;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;

        public WHO_Controller(int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _sourcefile = args.file_name;
            _processor = new WHO_Processor();
            _source = source;
            _file_base = source.local_folder;
            _source_id = source.id;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);
            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
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

            DateHelpers dh = new DateHelpers(_logging_helper);

            XmlSerializer writer = new XmlSerializer(typeof(WHORecord));
            DownloadResult res = new DownloadResult();

            using (var reader = new StreamReader(_sourcefile, true))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Configuration.HasHeaderRecord = false;
                    var records = csv.GetRecords<WHO_SourceRecord>();
                    _logging_helper.LogLine(" Rows loaded into WHO record structure");

                    // Consider each study in turn.

                    foreach (WHO_SourceRecord sr in records)
                    {
                        res.num_checked++;
                        WHORecord r = _processor.ProcessStudyDetails(sr, _logging_helper);

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

                            _file_writer.WriteWHOSourcedFile(writer, r, full_path);
                            bool added = _monitor_repo.UpdateStudyDownloadLog(r.source_id, r.sd_sid, r.remote_url, _saf_id,
                                                               dh.FetchDateTimeFromISO(r.record_date), full_path);
                            res.num_downloaded++;
                            if (added) res.num_added++;
                        }
                        
                        if (res.num_checked % 100 ==0) _logging_helper.LogLine(res.num_checked.ToString());
                    }
                }
            }
            return res;
        }
    }
}
