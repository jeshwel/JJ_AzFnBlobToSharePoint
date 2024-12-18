## Introduction 
Transfer large number of files both in volume and size from multiple containers in an Azure Storage Account to Sharepoint Document Library.

- Successfully tested large number of file transfer(s) with different file sizes.
- Successfully tested file transfers which contained multiple files around 2-3GB.

## AzFunc Flow Summary
Designed to enact a custom workflow like logic using Azure Functions.
1.	Step1-PrepItemsForFileProcessorQueue : Is a Time Trigger AzFunc that runs on a scheduled time and iterates through each container and adds blob info message to a queue to be processed by the next step.
2.	Step2-CopyFilesToSharePoint: Its a queue trigger that process each message and copy blobs to SharePoint. (Also, create SharePoint folders, if necessary.)
3.	Step3-Archive: Its a QueueTrigger, once the copy to SharePoint is completed the blobs are archived to an Archived container.
4.	MonitorDEPActivityStages: TimerTrigger AzFunc monitors whether a particular step has completed and initiates the next step. Wait queues have been designed so that triggers don't execute until a step is completed. Also this process checks for errors in any step and sends notification.

## Framework and Libraries
Main project framework: .net 4.8 (If the project need to be updated to latest .Net Core, the CSOM lib Basic Auth must be changed to Azure Entra Auth as latest CSOM lib does not support basic auth).

SharePoint Lib: Microsoft.SharePointOnline.CSOM

Activity Store: Azure Table Storage
