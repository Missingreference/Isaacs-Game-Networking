using System.Collections;
using System.Collections.Generic;
using System;
using MLAPI.Serialization;

namespace Elanetic.Network.Serialization
{
    public static class ReadWriteSystem
    {
        static public void WriteVersion(this BitWriter writer, Version version)
        {
            writer.WriteInt32(version.Major);
            writer.WriteInt32(version.Minor);
            writer.WriteInt32(version.Build);
            writer.WriteInt32(version.MajorRevision);
            writer.WriteInt32(version.MinorRevision);
        }

        static public Version ReadVersion(this BitReader reader)
        {
            int major = reader.ReadInt32();
            int minor = reader.ReadInt32();
            int build = reader.ReadInt32();
            int majorRevision = reader.ReadInt32();
            int minorRevision = reader.ReadInt32();

            //These if statements make it so that Version's unused properties(potentially Build, MajorRevision or MinorRevision)
            //are correctly -1 to match the written Version since passing anything less than zero into Version's constructor causes an exception
            if(build >= 0)
            {
                if(majorRevision >= 0)
                {
                    if(minorRevision >= 0)
                    {
                        return new Version(major, minor, build, (majorRevision << 16) + minorRevision);
                    }
                    return new Version(major, minor, build, majorRevision << 16);
                }
                return new Version(major, minor, build);
            }

            return new Version(major, minor);
        }

        static public void WriteDateTime(this BitWriter writer, DateTime dateTime)
        {
            writer.WriteInt64(dateTime.Ticks);
            writer.WriteInt32((int)dateTime.Kind);
        }

        static public DateTime ReadDateTime(this BitReader reader)
        {
            return new DateTime(reader.ReadInt64(), (DateTimeKind)reader.ReadInt32());
        }
    }
}