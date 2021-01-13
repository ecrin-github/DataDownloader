# DataDownloader
Downloads data from mdr data sources to local files, stored as XML.

The functioning of the mdr begins with the creation of a local copy of all the source data. A folder is set up to receive the data, one per source, and the data download process adds files to that folder. The download events are self contained and can take place independently of any further processing. The local copy of the source data simply grows with successive download events. At any point in time the folder holds *all* the data relevant to the mdr from its source, but because the basic details of each file, including the date and time of its download, are recorded in the monitoring database later processing stages can select subsets of files from that data store. Sources are trial registries and data repositories, and the download mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files from the data obtained (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converting the data into XML files (e.g. for WHO ICTRP data).
The format of the XML files created vary from source to source but represent the initial stage in the process of converting the source data into a consistent schema.<br/><br/>
The program represents the first stage in the 4 stage MDR extraction process:<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;**Download** => Harvest => Import => Aggregation<br/><br/>
For a much more detailed explanation of the extraction process,and the MDR system as a whole, please see the project wiki (landing page at https://ecrin-mdr.online/index.php/Project_Overview).<br/>
In particular, for the download prosesses, see<br/>
https://ecrin-mdr.online/index.php/Downloading_Data,<br/>
https://ecrin-mdr.online/index.php/Processing_PubMed_Data and <br/>
https://ecrin-mdr.online/index.php/Logging_and_Tracking<br/>

