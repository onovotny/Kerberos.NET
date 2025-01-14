﻿using Kerberos.NET.Asn1;
using Kerberos.NET.Crypto;
using System;
using System.Security.Cryptography;

namespace Kerberos.NET.Entities
{
    public partial class KrbApReq : IAsn1ApplicationEncoder<KrbApReq>
    {
        public KrbApReq()
        {
            ProtocolVersionNumber = 5;
            MessageType = MessageType.KRB_AP_REQ;
        }

        internal const int ApplicationTagValue = 14;

        public KrbApReq DecodeAsApplication(ReadOnlyMemory<byte> data)
        {
            return DecodeApplication(data);
        }

        internal static KrbApReq CreateApReq(
            KrbKdcRep tgsRep, 
            KerberosKey authenticatorKey, 
            ApOptions options, out KrbAuthenticator authenticator)
        {
            var ticket = tgsRep.Ticket;

            authenticator = new KrbAuthenticator
            {
                CName = tgsRep.CName,
                Realm = ticket.Realm,
                SequenceNumber = KerberosConstants.GetNonce(),
                Subkey = KrbEncryptionKey.Generate(authenticatorKey.EncryptionType),
                Checksum = KrbChecksum.EncodeDelegationChecksum(new DelegationInfo())
            };

            KerberosConstants.Now(out authenticator.CTime, out authenticator.CuSec);

            var apReq = new KrbApReq
            {
                Ticket = ticket,
                ApOptions = options,
                Authenticator = KrbEncryptedData.Encrypt(
                    authenticator.EncodeApplication(),
                    authenticatorKey,
                    KeyUsage.ApReqAuthenticator
                )
            };

            return apReq;
        }

        public ReadOnlyMemory<byte> EncodeGssApi()
        {
            var token = GssApiToken.Encode(Kerberos5Oid, this);

            var negoToken = new NegotiationToken
            {
                InitialToken = new NegTokenInit
                {
                    MechTypes = new[] { Kerberos5Oid },
                    MechToken = token
                }
            };

            return GssApiToken.Encode(SPNegoOid, negoToken);
        }

        private static readonly Oid Kerberos5Oid = new Oid(MechType.KerberosV5);
        private static readonly Oid SPNegoOid = new Oid(MechType.SPNEGO);

        public ReadOnlyMemory<byte> EncodeNegotiate()
        {
            var negoToken = new NegotiationToken
            {
                InitialToken = new NegTokenInit
                {
                    MechTypes = new[] { SPNegoOid },
                    MechToken = EncodeApplication()
                }
            };

            return negoToken.Encode().AsMemory();
        }
    }
}
