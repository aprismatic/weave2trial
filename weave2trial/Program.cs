using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aprismatic.ElGamalExt;

namespace weave2trial
{
    public static class Globals
    {
        public static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        public static readonly Random rnd = new();
        public static readonly int MEMBERS = 7;
        public static readonly int THRESHOLD = 3;
    }

    public static class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var fff = LinearSecretSharing.CreateSecretSharing(Globals.rng.NextBigInteger(), 10, Globals.rng);
        }
    }

    public enum State
    {
        NONE,
        STARTING_SUBMISSION
    };

    public class Node
    {
        public List<Node> addressBook;
        public List<Guid> supportedProtocols;

        private Random rnd = new();
        private ShamirShard N, d;
        private State _state;

        public void Init(IList<Node> contacts, ShamirShard N, ShamirShard d, State state)
        {
            addressBook = new List<Node>(contacts);
            supportedProtocols = new();
            supportedProtocols.Add(GroupSelectionProtocol.protocolId);
            this.N = N;
            this.d = d;
            this._state = state;
        }


        public void SubmitTo(Node to, BigInteger val)
        {
            if (addressBook.Count < Globals.THRESHOLD) throw new ApplicationException("Not enough connections");

            var subset = addressBook.OrderBy(_ => rnd.Next()).Take(Globals.THRESHOLD).ToList();
            var elGamal = new ElGamal(1024);

            var val_penc = elGamal.EncryptData(val);
        }

        /*public byte[] SubmissionRoutine(byte[] penc)
        {
            
        }*/

        // See Joy Algesheimer, Jan Camenisch, and Victor Shoup "Efficient Computation Modulo a Shared Secret with
        // Application to the Generation of Shared Safe-Prime Products", Sec 4.1 (https://link.springer.com/content/pdf/10.1007/3-540-45708-9_27.pdf)
        // Every member transforms their polynomial share into linear shares
        public void Poly2Additive()
        {

        }
    }

    public abstract class IProtocol
    {
        public static readonly Guid protocolId; 
        public void ReceiveMessage(IProtocolMessage msg);
    }

    public abstract class IProtocolMessage
    {
        public readonly IProtocol sender;
        public readonly Guid protocolId;

        public void Reply(IProtocolMessage msg)
        {
            sender.ReceiveMessage(msg);
        }
    }

    public class GroupSelectionProtocol : IProtocol
    {
        public enum State
        {
            WAITING_FOR_INVITATION,
            SENDING_INVITATIONS,
            WAITING_FOR_INVTIATION_ACCEPTS,
            SUCCESS,
            FAILURE
        }

        public State state;
        public static readonly Guid protocolId = Guid.NewGuid();
        public Queue<IProtocolMessage> messageQ = new();
        public Node Owner;

        public class InvitationMessage : IProtocolMessage { }
        public class InvitationAcceptMessage : IProtocolMessage { }
        public class InvitationRejectMessage : IProtocolMessage { }

        public readonly int groupSize;
        public IList<GroupSelectionProtocol> addrBook = new List<GroupSelectionProtocol>();
        public IList<GroupSelectionProtocol> group = new List<GroupSelectionProtocol>();

        public GroupSelectionProtocol(Node owner, int n, IList<GroupSelectionProtocol> contacts, State st = State.WAITING_FOR_INVITATION)
        {
            state = st;
            groupSize = n;
            addrBook = contacts;
            Owner = owner;
        }

        public void ReceiveMessage(IProtocolMessage msg)
        {
            messageQ.Enqueue(msg);
        }

        public void Tick()
        {
            switch (state)
            {
                case State.WAITING_FOR_INVITATION:
                    if (messageQ.Count > 0)
                    {
                        var msg = messageQ.Dequeue();
                        switch (msg)
                        {
                            case InvitationMessage im:
                                var reply = new InvitationAcceptMessage // add small probability to reject
                                {
                                    sender = this
                                };
                                im.Reply(reply);
                                state = State.SUCCESS;
                                break;
                            default: // log wrong message rcvd
                                break;
                        }
                    }
                    break;

                case State.SENDING_INVITATIONS:
                    addrBook.OrderBy(x => Globals.rnd.Next()).Take(groupSize).ToList().ForEach(x => // get random from addrBook
                        x.ReceiveMessage(new InvitationMessage { sender = this }));
                    state = State.WAITING_FOR_INVTIATION_ACCEPTS;
                    break;

                case State.WAITING_FOR_INVTIATION_ACCEPTS:
                    if (messageQ.Count > 0)
                    {
                        var msg = messageQ.Dequeue();
                        switch (msg)
                        {
                            case InvitationAcceptMessage iam:
                                if (iam.sender is GroupSelectionProtocol gsp)
                                {
                                    group.Add(gsp);
                                    // maybe remove gsp from addrbook
                                }
                                else
                                {
                                    throw new Exception($"{nameof(InvitationAcceptMessage)} message was sent not by {nameof(GroupSelectionProtocol)}");
                                }
                                break;

                            case InvitationRejectMessage:
                                // log this and do nothing or remove them from addrBook
                                break;

                            default:
                                throw new Exception($"Rcvd unexpected message of type {msg.GetType()}");
                        }
                    }
                    if (group.Count == groupSize)
                        state = State.SUCCESS;
                    break;
            }
        }
    }
}
