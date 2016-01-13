using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using Helpers;
using Models.ClientPartition;
using Newtonsoft.Json;
using Services.Client;
using LogicalVolume = Models.ImageSchema.LogicalVolume;

namespace BLL.ClientPartitioning
{
    /// <summary>
    ///     Calculates the minimum sizes required for various hard drives / partitions for the client imaging process  
    /// </summary>
    public class ClientPartitionHelper
    {
        private readonly Models.ImageSchema.ImageSchema _imageSchema;

        public ClientPartitionHelper(Models.ImageProfile imageProfile)
        {
            string schema = null;
     
            if (imageProfile != null)
            {
                if (imageProfile.PartitionMethod == "Dynamic" && !string.IsNullOrEmpty(imageProfile.CustomSchema))
                {
                    schema = imageProfile.CustomSchema;
                }
                else
                {
                    var path = Settings.PrimaryStoragePath + "images" + Path.DirectorySeparatorChar + imageProfile.Image.Name + Path.DirectorySeparatorChar +
                               "schema";
                    if (File.Exists(path))
                    {
                        using (var reader = new StreamReader(path))
                        {
                            schema = reader.ReadLine() ?? "";
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(schema))
            {
                _imageSchema = JsonConvert.DeserializeObject<Models.ImageSchema.ImageSchema>(schema);
            }
        }

        /// <summary>
        ///     Calculates the minimum block size for an extended partition by determining the minimum size of all logical
        ///     partitions that fall under the extended.  Does not assume that any extended partitions actually exist.
        /// </summary>
        public ExtendedPartitionHelper ExtendedPartition(int hdNumberToGet)
        {
            var lbsByte = _imageSchema.HardDrives[hdNumberToGet].Lbs;
            var extendedPartitionHelper = new ExtendedPartitionHelper();

            //Determine if any Extended or Logical Partitions are present.  Needed ahead of time correctly calculate sizes.
            //And calculate minimum needed extended partition size

            bool schemaHasExtendedPartition = false;
            string logicalFsType = null;
            foreach (var part in _imageSchema.HardDrives[hdNumberToGet].Partitions.Where(part => part.Active))
            {
                if (part.Type.ToLower() == "extended")
                {
                    schemaHasExtendedPartition = true;
                }
                if (part.Type.ToLower() != "logical") continue;
                extendedPartitionHelper.LogicalCount++;
                logicalFsType = part.FsType;
                extendedPartitionHelper.HasLogical = true;
            }

            if (!schemaHasExtendedPartition) return extendedPartitionHelper;



            if (logicalFsType != null &&
                (extendedPartitionHelper.LogicalCount == 1 && logicalFsType.ToLower() == "swap"))
                extendedPartitionHelper.IsOnlySwap = true;

            var partitionCounter = -1;
            foreach (var partition in _imageSchema.HardDrives[hdNumberToGet].Partitions)
            {
                partitionCounter++;
                if (!partition.Active)
                    continue;
                if (partition.Type.ToLower() == "extended")
                {
                    if (partition.ForceFixedSize)
                    {
                        extendedPartitionHelper.MinSizeBlk = partition.Size;
                        return extendedPartitionHelper;                       
                    }
                }
                if (extendedPartitionHelper.HasLogical)
                {
                    if (partition.Type.ToLower() != "logical") continue;

                    //Check if the logical partition is a physical volume for a volume group
                    if (partition.FsId.ToLower() == "8e" || partition.FsId.ToLower() == "8e00")
                    {
                        //Add to the minimum extended size to the minimum size of the volume group
                        var volumeGroupHelper = VolumeGroup(hdNumberToGet, partitionCounter);
                        extendedPartitionHelper.MinSizeBlk += volumeGroupHelper.MinSizeBlk;

                        if (volumeGroupHelper.MinSizeBlk == 0)
                            extendedPartitionHelper.MinSizeBlk += partition.Size;
                    }
                    else
                    {
                        //Extended partition size overridden by user
                        if (!string.IsNullOrEmpty(partition.CustomSize))
                            extendedPartitionHelper.MinSizeBlk += Convert.ToInt64(partition.CustomSize);
                        else
                        {
                            //if logical partition is not resizable use the partition size created during upload
                            if (partition.VolumeSize == 0)
                                extendedPartitionHelper.MinSizeBlk += partition.Size;
                            else
                            {
                                //The resize value and used_mb value are calculated during upload by two different methods
                                //Use the one that is bigger just in case.
                                if (partition.VolumeSize > partition.UsedMb)
                                    extendedPartitionHelper.MinSizeBlk += partition.VolumeSize*1024*1024/lbsByte;
                                else
                                    extendedPartitionHelper.MinSizeBlk += partition.UsedMb*1024*1024/lbsByte;
                            }
                        }
                    }
                }
                //If Hd has extended but no logical, use the extended to calc size
                else
                {
                    //In this case someone has defined an extended partition but has not created any logical
                    //This could just be for preperation of leaving room for more logical partition later
                    //This should be highly unlikely but should account for it anyway.  There is no way of knowing a minimum size required
                    //while still having the partition be resizable.  So will set minimum sized required to unless user overrides
                    if (partition.Type.ToLower() != "extended") continue;

                    //extended partition size set by user
                    if (!string.IsNullOrEmpty(partition.CustomSize))
                        extendedPartitionHelper.MinSizeBlk = Convert.ToInt64(partition.CustomSize);
                    else
                    //set arbitary minimum to 100MB
                        extendedPartitionHelper.MinSizeBlk = 100*1024*1024/lbsByte;

                    break; //Like the Highlander there can be only one extended partition
                }
            }

            //Logical paritions default to 1MB more than the previous block using fdisk. This needs to be added to extended size so logical parts will fit inside
            long epPadding = (((1048576/lbsByte)*extendedPartitionHelper.LogicalCount) + (1048576/lbsByte));
            extendedPartitionHelper.MinSizeBlk += epPadding;
            return extendedPartitionHelper;
        }

        /// <summary>
        ///     Calculates the smallest size hard drive in Bytes that can be used to deploy the image to, based off the data usage.
        ///     The newHdSize parameter is arbitrary but is used to determine if the hard being deployed to is the same size that
        ///     the image was created from.
        /// </summary>
        public long HardDrive(int hdNumberToGet, long newHdSize = 0)
        {
            long minHdSizeRequiredBlk = 0;
            var lbsByte = _imageSchema.HardDrives[hdNumberToGet].Lbs;

            //if hard drive is the same size as original, then no need to calculate.  It will fit.
            if (_imageSchema.HardDrives[hdNumberToGet].Size*lbsByte == newHdSize)
                return newHdSize;

            var partitionCounter = -1;
            foreach (var partition in _imageSchema.HardDrives[hdNumberToGet].Partitions)
            {
                partitionCounter++;
                if (!partition.Active) continue;
                //Logical partitions are calculated via the extended
                if (partition.Type.ToLower() == "logical") continue;
                minHdSizeRequiredBlk += this.Partition(hdNumberToGet, partitionCounter, newHdSize).MinSizeBlk;
            }
            return minHdSizeRequiredBlk*lbsByte;
        }

        /// <summary>
        ///     Calculates the minimum block size required for a single logical volume, assuming the logical volume cannot have any
        ///     children.
        /// </summary>
        public PartitionHelper LogicalVolume(LogicalVolume lv, int lbsByte)
        {
            var logicalVolumeHelper = new PartitionHelper {MinSizeBlk = 0};
            if (lv.ForceFixedSize)
            {
                logicalVolumeHelper.MinSizeBlk = lv.Size;
                logicalVolumeHelper.IsDynamicSize = false;
            }
            else if (!string.IsNullOrEmpty(lv.CustomSize))
            {
                long customSizeBytes = 0;
                switch (lv.CustomSizeUnit)
                {
                    case "MB":
                        customSizeBytes = Convert.ToInt64(lv.CustomSize)*1024*1024;
                        break;
                    case "GB":
                        customSizeBytes = Convert.ToInt64(lv.CustomSize) * 1024 * 1024 * 1024;
                        break;
                }
                logicalVolumeHelper.MinSizeBlk = customSizeBytes / lbsByte;
                logicalVolumeHelper.IsDynamicSize = false;
            }
            else
            {
                logicalVolumeHelper.IsDynamicSize = true;
                if (lv.VolumeSize > lv.UsedMb)
                    logicalVolumeHelper.MinSizeBlk = lv.VolumeSize*1024*1024/lbsByte;
                else
                    logicalVolumeHelper.MinSizeBlk = lv.UsedMb*1024*1024/lbsByte;
            }

            return logicalVolumeHelper;
        }

        /// <summary>
        ///     Calculates the minimum block size required for a single partition, taking into account any children partitions.
        /// </summary>
        public PartitionHelper Partition(int hdNumberToGet, int partNumberToGet, long newHdSize)
        {
            var partitionHelper = new PartitionHelper {MinSizeBlk = 0};
            var extendedPartitionHelper = ExtendedPartition(hdNumberToGet);
            var partition = _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet];
            partitionHelper.VolumeGroupHelper = VolumeGroup(hdNumberToGet, partNumberToGet);
            var lbsByte = _imageSchema.HardDrives[hdNumberToGet].Lbs;

            
            //Look if any volume groups are present for this partition.  If so set the volumesize for the volume group to the minimum size
            //required for the volume group.  Volume groups are always treated as resizable even if none of the individual 
            //logical volumes are resizable
            if (partitionHelper.VolumeGroupHelper.Pv != null)
            {
                partitionHelper.PartitionHasVolumeGroup = true;
                partition.VolumeSize = (partitionHelper.VolumeGroupHelper.MinSizeBlk*lbsByte/1024/1024);
            }


            if (partition.ForceFixedSize)
            {
                partitionHelper.MinSizeBlk = partition.Size;
                partitionHelper.IsDynamicSize = false;
            }
            //Use partition size that user has set for the partition, if it is set.
            else if (!string.IsNullOrEmpty(partition.CustomSize))
            {
                long customSizeBytes = 0;
                switch (partition.CustomSizeUnit)
                {
                    case "MB":
                        customSizeBytes = Convert.ToInt64(partition.CustomSize) * 1024 * 1024;
                        break;
                    case "GB":
                        customSizeBytes = Convert.ToInt64(partition.CustomSize) * 1024 * 1024 * 1024;
                        break;
                    case "%":
                        double hdPercent = Convert.ToDouble(partition.CustomSize) / 100;
                        customSizeBytes = Convert.ToInt64(hdPercent * newHdSize);
                        break;
                }
                partitionHelper.MinSizeBlk = customSizeBytes/lbsByte;
                partitionHelper.IsDynamicSize = false;
            }

            //If partition is not resizable.  Determine partition size.  Also if the partition is less than 5 gigs assume it is that
            // size for a reason, do not resize it even if it is marked as a resizable partition
            else if ((partition.VolumeSize == 0 && partition.Type.ToLower() != "extended") ||
                     (partition.Type.ToLower() == "extended" && extendedPartitionHelper.IsOnlySwap) ||
                     partition.Size * lbsByte <= 5368709120 || partition.FsType == "swap")
            {
                partitionHelper.MinSizeBlk = partition.Size;
                partitionHelper.IsDynamicSize = false;
            }
            //If resizable determine what percent of drive partition was originally and match that to the new drive
            //while making sure the min size is still less than the resized size.
            else
            {
                partitionHelper.IsDynamicSize = true;
                if (partition.Type.ToLower() == "extended")
                    partitionHelper.MinSizeBlk = extendedPartitionHelper.MinSizeBlk;
                else if (partitionHelper.VolumeGroupHelper.Pv != null)
                {
                    partitionHelper.MinSizeBlk = partitionHelper.VolumeGroupHelper.MinSizeBlk;
                }
                else
                {
                    //The resize value and used_mb value are calculated during upload by two different methods
                    //Use the one that is bigger just in case.
                  
                    if (partition.VolumeSize > partition.UsedMb)
                        partitionHelper.MinSizeBlk = partition.VolumeSize*1024*1024/lbsByte;
                    else
                        partitionHelper.MinSizeBlk = (partition.UsedMb*1024*1024)/lbsByte;
                }
            }

            return partitionHelper;
        }

        /// <summary>
        ///     Calculates the minimum block size required for a volume group by determine the size of each
        ///     logical volume within the volume group.  Does not assume that any volume group actually exists.
        /// </summary>
        public ClientVolumeGroupHelper VolumeGroup(int hdNumberToGet, int partNumberToGet)
        {
            var lbsByte = _imageSchema.HardDrives[hdNumberToGet].Lbs;

            var volumeGroupHelper = new ClientVolumeGroupHelper
            {
                MinSizeBlk = 0,
                HasLv = false
            };

            if (_imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].FsId.ToLower() != "8e" &&
                _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].FsId.ToLower() != "8e00") return volumeGroupHelper;
            if (!_imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].Active)
                return volumeGroupHelper;

            //if part.vg is null, most likely version 2.3.0 beta1 before lvm was added.
            if (_imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup == null) return volumeGroupHelper;
            //if vg.name is null partition was uploaded at physical partion level, by using the shrink_lvm=false flag
            if (_imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup.Name == null) return volumeGroupHelper;
            volumeGroupHelper.Name = _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup.Name;
            foreach (var logicalVolume in _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup.LogicalVolumes)
            {
                if (!logicalVolume.Active)
                    continue;
                volumeGroupHelper.HasLv = true;
                volumeGroupHelper.Pv = _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup.PhysicalVolume;
                //Logical volume size overridden by user
                if (!string.IsNullOrEmpty(logicalVolume.CustomSize))
                    volumeGroupHelper.MinSizeBlk += Convert.ToInt64(logicalVolume.CustomSize);
                else
                {
                    //If logical volume is not resizable use the actual size of the logical volume during upload
                    if (logicalVolume.VolumeSize == 0)
                        volumeGroupHelper.MinSizeBlk += logicalVolume.Size;
                    else
                    {
                        //The resize value and used_mb value are calculated during upload by two different methods
                        //Use the one that is bigger just in case.
                        if (logicalVolume.VolumeSize > logicalVolume.UsedMb)
                            volumeGroupHelper.MinSizeBlk += logicalVolume.VolumeSize*1024*1024/lbsByte;
                        else
                            volumeGroupHelper.MinSizeBlk += logicalVolume.UsedMb*1024*1024/lbsByte;
                    }
                }
            }

            if (volumeGroupHelper.HasLv) return volumeGroupHelper;

            //Could Have VG Without LVs
            //Set arbitrary minimum size to 100mb
            volumeGroupHelper.Pv = _imageSchema.HardDrives[hdNumberToGet].Partitions[partNumberToGet].VolumeGroup.PhysicalVolume;
            volumeGroupHelper.MinSizeBlk = 100*1024*1024/lbsByte;
            return volumeGroupHelper;
        }

        public int NextActiveHardDrive(List<int> schemaImagedDrives, int clientHdNumber )
        {

        

            //Look for first active hard drive image
            if (_imageSchema.HardDrives[clientHdNumber].Active)
            {
                return clientHdNumber;
            }
            else
            {
                var hardDriveCounter = 0;
                while (hardDriveCounter <= _imageSchema.HardDrives.Count())
                {
                    if (_imageSchema.HardDrives[hardDriveCounter].Active && !schemaImagedDrives.Contains(hardDriveCounter))
                    {
                        return hardDriveCounter;
                    }

                    hardDriveCounter++;
                }

                return -1;
            }
        }

        public List<Services.Client.PhysicalPartition> GetActivePartitions(int schemaHdNumber, Models.ImageProfile imageProfile)
        {
            var listPhysicalPartition = new List<Services.Client.PhysicalPartition>();
            var imagePath = Settings.PrimaryStoragePath + Path.DirectorySeparatorChar + "images" + Path.DirectorySeparatorChar + imageProfile.Image.Name + Path.DirectorySeparatorChar + "hd" +
                           schemaHdNumber;
            foreach (var partition in _imageSchema.HardDrives[schemaHdNumber].Partitions.Where(partition => partition.Active))
            {
                var physicalPartition = new Services.Client.PhysicalPartition();
                physicalPartition.Number = partition.Number;
                physicalPartition.Guid = partition.Guid;
                physicalPartition.Uuid = partition.Uuid;
                physicalPartition.FileSystem = partition.FsType;
                physicalPartition.Type = partition.Type;
                physicalPartition.EfiBootLoader = partition.EfiBootLoader;
                string imageFile = null;
                foreach (var ext in new[] {"ntfs", "fat", "extfs", "hfsp", "imager", "xfs"})
                {
                    imageFile =
                        Directory.GetFiles(
                            imagePath + Path.DirectorySeparatorChar, "part" + partition.Number + "." + ext + ".*")
                            .FirstOrDefault();
                    physicalPartition.PartcloneFileSystem = ext;
                    if (imageFile != null) break;
                }
                switch (Path.GetExtension(imageFile))
                {
                    case ".lz4":
                        physicalPartition.Compression = "lz4";
                        break;
                    case ".gz":
                        physicalPartition.Compression = "gz";
                        break;
                    case ".uncp":
                        physicalPartition.Compression = "uncp";
                        break;
                    default:
                        physicalPartition.Compression = "null";
                        break;
                }

                if (partition.VolumeGroup.Name != null)
                {
                    physicalPartition.VolumeGroup = new Services.Client.VolumeGroup();
                    physicalPartition.VolumeGroup.Name = partition.VolumeGroup.Name;
                    var listLogicalVolumes = new List<Services.Client.LogicalVolume>();
                    var logicalVolumeCounter = 0;
                    foreach (var logicalVolume in partition.VolumeGroup.LogicalVolumes.Where(x => x.Active))
                    {
                        logicalVolumeCounter++;
                        var clientLogicalVolume = new Services.Client.LogicalVolume();
                        clientLogicalVolume.Name = logicalVolume.Name;
                        clientLogicalVolume.FileSystem = logicalVolume.FsType;
                        clientLogicalVolume.Uuid = logicalVolume.Uuid;


                        foreach (var ext in new[] {"ntfs", "fat", "extfs", "hfsp", "imager", "xfs"})
                        {
                            imageFile =
                                Directory.GetFiles(
                                    imagePath + Path.DirectorySeparatorChar,
                                    partition.VolumeGroup.Name + "-" + logicalVolume.Name +"." + ext + ".*")
                                    .FirstOrDefault();
                            clientLogicalVolume.PartcloneFileSystem = ext;
                            if (imageFile != null) break;
                        }
                        switch (Path.GetExtension(imageFile))
                        {
                            case ".lz4":
                                clientLogicalVolume.Compression = "lz4";
                                break;
                            case ".gz":
                                clientLogicalVolume.Compression = "gz";
                                break;
                            case ".uncp":
                                clientLogicalVolume.Compression = "uncp";
                                break;
                            default:
                                clientLogicalVolume.Compression = "null";
                                break;
                        }

                        listLogicalVolumes.Add(clientLogicalVolume);
                    }
                    physicalPartition.VolumeGroup.LogicalVolumeCount = logicalVolumeCounter;
                    physicalPartition.VolumeGroup.LogicalVolumes = listLogicalVolumes;
                }
                listPhysicalPartition.Add(physicalPartition);
                
            }

            return listPhysicalPartition;
        }

        public int GetActivePartitionCount(int schemaHdNumber)
        {
            return _imageSchema.HardDrives[schemaHdNumber].Partitions.Count(partition => partition.Active);
        }

        public string CheckForLvm(int schemaHdNumber)
        {
            return (from part in _imageSchema.HardDrives[schemaHdNumber].Partitions
                where part.Active
                where part.VolumeGroup != null
                where part.VolumeGroup.LogicalVolumes != null
                select part).Any() ? "true" : "false";
        }

        public Models.ImageSchema.ImageSchema GetImageSchema()
        {
            return _imageSchema;
        }
    }
}