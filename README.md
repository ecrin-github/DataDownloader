# DataDownloader
Downloads data from mdr data sources to local files, stored as XML.

The functioning of the mdr begins with the creation of a local copy of all the source data. A folder is set up to receive the data, one per source, and the data download process adds files to that folder. The download events are self contained and can take place independently of any further processing. The local copy of the source data simply grows with successive download events. At any point in time the folder holds *all* the data relevant to the mdr from its source, but because the basic details of each file, including the date and time of its download, are recorded later processing stages can select subsets of files from that data store. Sources are trial registries and data repositories, and the download mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files from the data obtained (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converting the data into XML files (e.g. for WHO ICTRP data).
The format of the XML files created vary from source to source but represent the initial stage in the process of converting the source data into a consistent schema.<br/><br/>
The program represents the first stage in the 4 stage MDR extraction process:<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;**Download** => Harvest => Import => Aggregation<br/><br/>
For a much more detailed explanation of the extraction process,and the MDR system as a whole, please see the project wiki (landing page at https://ecrin-mdr.online/index.php/Project_Overview).<br/>

### Parameters
The system is a console app, and takes the following parameters:<br/>
**-s**, followed by an integer: the id of the source to be downloaded, e.g. 100120 = ClinicalTrials.gov.<br/>
**-t**, followed by an integer: the id of the type of fetch (or sometimes search) - see below for more details.<br/>
**-f**, followed by a file path: the path should be that of the source fiule for those download types that require it.<br/>
**-d**, followed by a date, in the ISO yyyy-MM-dd format: the date should be the cut-off date for those download data types that require one.<br/>
**-q**, followed by an integer: the integer id of a listed query for using within an API<br/>
**-p**, followed by a string of comma delimited integers: the ids of the previous searches that should be used as the basis of this download.<br/>
**-L**: a flag indicating that no logging should take place. Usefuil in some testing and development scenarios.<br/>

### Download Types
The ranghe of parameters illustrate the need for the variety of approaches required to deal with the various types of source material. The types of download available are:
101	All records (download)	
*Identifies and downloads XML files, one per record, from the entire data source. All available files are downloaded.*	

102	All records (scrape)	
*Scrapes data, creates XML files, one per record, from the entire data source. All available records are processed.*	

103	All records (file)	
*A local file (e.g. CSV file downloaded from WHO) used as the data source. Records transformed into local XML files. Requires file path*

111	All new or revised records (download)	
*Identifies and downloads XML files, one per each record that has been added new or revised since the given cutoff date, within the entire data source.	Requires cut off date*

112	All new or revised records (scrape)	
*Scrapes data and creates XML files, one per record, from  data that is new or revised since the given cutoff data, inspecting the entire data source. Requires cut off date*

113	All new or revised records (file)	
*Uses a downloaded file, with new and / or revised data since the last data fetch (usually as provided by the source). Local XML files created or amended. Requires file path*

114	All new or revised records with a filter applied (download)	
*Identifies and downloads XML files, one per each record that has been added new or revised since the given cutoff date, within a filtered source record set.	Requires cut off date, query id*

121	Filtered records (download)	
*Identifies and downloads XML files, one per record, that meet specific search criteria (excluding revision date). The search type should be identified.	Requires query id

122	Filtered records (scrape)	
*Scrapes data and creates XML files, one per record, from data that meets specific search criteria (excluding revision date). The search type should be identified.	Requires query id*

123	Filtered records (file)	
*Uses a downloaded file, containing data that meets specific search criteria (excluding those based on revision date). Local XML files created or amended. The search type should be identified.	Requires file path, query id*

131	Records from prior search (download)	
*Downloads XML files, one per record, that have been previously identified by a search as requiring download. The search(es) should be identified.	Requires previous search id(s)*

132	Records from prior search (scrape)	Scrapes data, and creates or amends XML files, one per record, that have been previously identified by a search as requiring download. *The search(es) should be identified.	Requires previous search id(s)*

133	Records from prior search (file)	
*Uses a downloaded file and extracts from it data that has previously been identified as required by a search. The search(es) should be identified.	requires file path, previous search id(s)*

134	Records from prior search and new or revised records (download)	
*Downloads XML files, one per record, that have been previously identified by a search as requiring download, and / or that have been revised or added since the cutoff date. The search(es) should be identified.	requires cut off date, previous search id(s)*

141	Assumed incomplete records (download)	
*Identifies and downloads XML files, one per each record, that are assumed to be incomplete (using source specific criteria), within the entire data source.*

142	Assumed incomplete records (scrape)	
*Scrapes data and creates XML files, one per record, from  data that is assumed to be incomplete (using source-specific criteria), inspecting the entire data source.	*

143	Assumed incomplete records (file)	
*Uses a downloaded file, with data that is assumed to be incomplete (using source specific criteria). Local XML files created or amended.	Requires file path*

201	Full search (records located only)	
*Identifies data or web page, including its source URL, across the entire data source, for later fetch.*	

202	New or revised records (records located only)	
*Identifies data or web page, including its source URL, that meet the criteria of having been revised or added since the given cutoff date. For later fetch. Requires cut off date*

203	Filtered search (records located only)	
*Identifies data or web page, including its source URL, that meet the criteria of a focused search, for later fetch. The search type should be specified.	Requires query id*

204	New or revised records and filtered search  (records located only)	
*Identifies data or web page, including its source URL, that meet the criteria of a focused search, AND which have also been revised or added since the given cutoff date, for later fetch.	Requires cut off date, query id*

205	Search using MDR data (records located only)	
*Identifies data or web pages, including their source URLs, where previously processed MDR data indicates it should be fetched, e.g. references in one source to another.	Requires query id*


### Provenance
* Author: Steve Canham
* Organisation: ECRIN (https://ecrin.org)
* System: Clinical Research Metadata Repository (MDR)
* Project: EOSC Life
* Funding: EU H2020 programme, grant 824087

