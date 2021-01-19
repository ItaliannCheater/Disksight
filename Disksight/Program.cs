using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Disksight {
    class Program {

        class PartitionInfo {
            public PartitionInfo(char letter, bool isMounted) {
                this.Letter = letter;
                this.IsMounted = isMounted;
            }

            public char Letter { get; }
            public bool IsMounted { get; }
        }

        class DiskLog {
            public DiskLog(string name, DateTime? generatedAt, long? recordIdentifier) {
                this.Name = name;
                this.Time = generatedAt;
                this.Id = recordIdentifier;
            }

            public string Name { get; }
            public DateTime? Time { get; }
            public long? Id { get; }
        }

        static void Main(string[] args) {
            List<PartitionInfo> partitionsInfo = new List<PartitionInfo>();
            getPartitions().ForEach(p => partitionsInfo.Add(new PartitionInfo(p, isMounted(p))));
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("Partitions\n-------------------------------\n");
            partitionsInfo.ForEach(i => {
                Console.WriteLine("Letter: {0}\n => Mounted: {1}", i.Letter, i.IsMounted);
            });

            Console.WriteLine();

            Console.WriteLine("USB storages\n-------------------------------\n");
            getRemovableStorages().ForEach(s => Console.WriteLine(s));

            Console.WriteLine();

            Console.WriteLine("Disks logs\n-------------------------------\n");
            getDisksLogs().ForEach(l => {
                Console.WriteLine("Disk name: {0}\n => Generated at: {1}\n => Record ID: {2}",
                    l.Name, l.Time, l.Id);
            });

            Console.Write("\n\nPress ENTER to exit the program...");
            Console.ReadLine();
        }

        static List<char> getPartitions() {
            List<char> partitions = new List<char>();
            Regex regex = new Regex(@"^\\DosDevices\\(\w):$");
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\MountedDevices");
            string[] values = key.GetValueNames();

            foreach (string v in values) {
                Match match = regex.Match(v);

                if (!match.Success) continue;

                string partition = match.Groups[1].Value;
                partitions.Add(Convert.ToChar(partition));
            }

            return partitions;
        }

        static List<string> getRemovableStorages() {
            List<string> storages = new List<string>();
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR");
            string[] storagesKeys = key.GetSubKeyNames();

            storagesKeys.ToList().ForEach(k => {
                RegistryKey storageKey = key.OpenSubKey(k);
                RegistryKey storageInfoKey = storageKey.OpenSubKey(storageKey.GetSubKeyNames()[0]);
                string storage = storageInfoKey.GetValue("FriendlyName").ToString();
                storages.Add(storage);
            });

            return storages;
        }

        static List<DiskLog> getDisksLogs() {
            EventRecord entry;
            List<DiskLog> disksLogs = new List<DiskLog>();
            string logPath = @"C:\Windows\System32\winevt\Logs\Microsoft-Windows-StorageSpaces-Driver%4Operational.evtx";
            EventLogReader logReader = new EventLogReader(logPath, PathType.FilePath);
            DateTime pcStartTime = startTime();

            while ((entry = logReader.ReadEvent()) != null) {
                if (entry.Id != 207) continue;
                if (entry.TimeCreated <= pcStartTime) continue;

                IList<EventProperty> properties = entry.Properties;
                string driveManufacturer = properties[3].Value.ToString();
                string driveModelNumber = properties[4].Value.ToString();

                if (driveManufacturer == "NULL") driveManufacturer = "";
                else driveManufacturer += " ";

                disksLogs.Add(new DiskLog($"{driveManufacturer}{driveModelNumber}",
                    entry.TimeCreated, entry.RecordId));
            }

            return disksLogs;
        }

        static bool isMounted(char partition) {
            return Directory.Exists($"{partition}:");
        }

        static DateTime startTime() {
            return DateTime.Now.AddMilliseconds(-Environment.TickCount);
        }
    }
}
