﻿using System;
using System.IO;

namespace Kerberos.NET.Entities
{
    [Flags]
    public enum ChecksumFlag
    {
        GSS_C_DELEG_FLAG = 1 << 0,
        GSS_C_MUTUAL_FLAG = 1 << 1,
        GSS_C_REPLAY_FLAG = 1 << 2,
        GSS_C_SEQUENCE_FLAG = 1 << 3,
        GSS_C_CONF_FLAG = 1 << 4,
        GSS_C_INTEG_FLAG = 1 << 5,
        GSS_C_ANON_FLAG = 1 << 6,
        GSS_C_PROT_READY_FLAG = 1 << 7,
        GSS_C_TRANS_FLAG = 1 << 8,

        GSS_C_DCE_STYLE = 1 << 12,
        GSS_C_IDENTIFY_FLAG = 1 << 13,
        GSS_C_EXTENDED_ERROR_FLAG = 1 << 14
    }

    public class DelegationInfo
    {
        public const int GSS_C_AF_NETBIOS = 0x14;

        public const int ChannelBindingLength = 0x10;

        private const ChecksumFlag DefaultFlags =
                        ChecksumFlag.GSS_C_MUTUAL_FLAG |
                        ChecksumFlag.GSS_C_REPLAY_FLAG |
                        ChecksumFlag.GSS_C_SEQUENCE_FLAG |
                        ChecksumFlag.GSS_C_CONF_FLAG |
                        ChecksumFlag.GSS_C_INTEG_FLAG |
                        ChecksumFlag.GSS_C_EXTENDED_ERROR_FLAG;

        public ReadOnlyMemory<byte> Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                if (ChannelBinding == null)
                {
                    ChannelBinding = new byte[ChannelBindingLength];
                }

                writer.Write(ChannelBinding?.Length ?? 0);
                writer.Write(ChannelBinding);

                writer.Write((int)Flags);

                // TODO: Encode DelegationTicket

                return stream.ToArray();
            }
        }

        public DelegationInfo Decode(ReadOnlyMemory<byte> value)
        {
            using (var reader = new BinaryReader(new MemoryStream(value.ToArray())))
            {

                Length = reader.ReadInt32();

                ChannelBinding = reader.ReadBytes(Length);

                Flags = (ChecksumFlag)reader.ReadBytes(4).AsLong(littleEndian: true);

                if (reader.BytesAvailable() > 0)
                {
                    DelegationOption = reader.ReadInt16();
                }

                int delegationLength = 0;

                if (reader.BytesAvailable() > 0)
                {
                    delegationLength = reader.ReadInt16();
                }

                byte[] delegationTicket = null;

                if (reader.BytesAvailable() > 0)
                {
                    delegationTicket = reader.ReadBytes(delegationLength);
                }

                if (delegationTicket != null && delegationTicket.Length > 0)
                {
                    DelegationTicket = KrbCred.DecodeApplication(delegationTicket);
                }

                if (reader.BytesAvailable() > 0)
                {
                    Extensions = reader.ReadBytes((int)reader.BytesAvailable());
                }
            }

            return this;
        }

        public int Length { get; set; }

        public byte[] ChannelBinding { get; set; }

        public ChecksumFlag Flags { get; set; } = DefaultFlags;

        public int DelegationOption { get; set; }

        public KrbCred DelegationTicket { get; set; }

        public byte[] Extensions { get; set; }
    }
}
