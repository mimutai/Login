using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Login
{
    class NFC_Reader
    {
        //最初に実行
        [DllImport("winscard.dll")]
        public static extern uint SCardEstablishContext(uint dwScope, IntPtr pvReserved1, IntPtr pvReserved2, out IntPtr phContext);

        [DllImport("winscard.dll", EntryPoint = "SCardListReadersW", CharSet = CharSet.Unicode)]
        public static extern uint SCardListReaders(
        IntPtr hContext, byte[] mszGroups, byte[] mszReaders, ref UInt32 pcchReaders);

        //カードリーダーとカードの状態、およびその変化の検出
        [DllImport("winscard.dll", EntryPoint = "SCardGetStatusChangeW", CharSet = CharSet.Unicode)]
        public static extern uint SCardGetStatusChange(IntPtr hContext, int dwTimeout, [In, Out] SCARD_READERSTATE[] rgReaderStates, int cReaders);

        //カードリーダーの状態
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SCARD_READERSTATE
        {
            /// <summary>
            /// Reader
            /// </summary>
            internal string szReader;
            /// <summary>
            /// User Data
            /// </summary>
            internal IntPtr pvUserData;
            /// <summary>
            /// Current State
            /// </summary>
            internal UInt32 dwCurrentState;
            /// <summary>
            /// Event State/ New State
            /// </summary>
            internal UInt32 dwEventState;
            /// <summary>
            /// ATR Length
            /// </summary>
            internal UInt32 cbAtr;
            /// <summary>
            /// Card ATR
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
            internal byte[] rgbAtr;
        }

        //カードと接続
        [DllImport("winscard.dll", EntryPoint = "SCardConnectW", CharSet = CharSet.Unicode)]
        public static extern uint SCardConnect(IntPtr hContext, string szReader, uint dwShareMode, uint dwPreferredProtocols, ref IntPtr phCard, ref IntPtr pdwActiveProtocol);

        //データ送受信
        [DllImport("winscard.dll")]
        public static extern uint SCardTransmit(IntPtr hCard, IntPtr pioSendRequest, byte[] SendBuff, int SendBuffLen, SCARD_IO_REQUEST pioRecvRequest,
        byte[] RecvBuff, ref int RecvBuffLen);

        [StructLayout(LayoutKind.Sequential)]
        internal class SCARD_IO_REQUEST
        {
            internal uint dwProtocol;
            internal int cbPciLength;
            public SCARD_IO_REQUEST()
            {
                dwProtocol = 0;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        public static extern void FreeLibrary(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr handle, string procName);

        //接続を解除する
        [DllImport("WinScard.dll")]
        public static extern uint SCardDisconnect(IntPtr hCard, int Disposition);

        //最後に実行
        [DllImport("Winscard.dll")]
        public static extern uint SCardReleaseContext(IntPtr phContext);

        //Const
        public const uint SCARD_S_SUCCESS = 0;
        public const uint SCARD_E_NO_SERVICE = 0x8010001D;
        public const uint SCARD_E_TIMEOUT = 0x8010000A;

        public const uint SCARD_SCOPE_USER = 0;
        public const uint SCARD_SCOPE_TERMINAL = 1;
        public const uint SCARD_SCOPE_SYSTEM = 2;

        public const int SCARD_STATE_UNAWARE = 0x0000;
        public const int SCARD_STATE_CHANGED = 0x00000002;// This implies that there is a
        // difference between the state believed by the application, and
        // the state known by the Service Manager.  When this bit is set,
        // the application may assume a significant state change has
        // occurred on this reader.
        public const int SCARD_STATE_PRESENT = 0x00000020;// This implies that there is a card
        // in the reader.
        public const UInt32 SCARD_STATE_EMPTY = 0x00000010;  // This implies that there is not
                                                             // card in the reader.  If this bit is set, all the following bits will be clear.

        public const int SCARD_SHARE_SHARED = 0x00000002; // - This application will allow others to share the reader
        public const int SCARD_SHARE_EXCLUSIVE = 0x00000001; // - This application will NOT allow others to share the reader
        public const int SCARD_SHARE_DIRECT = 0x00000003; // - Direct control of the reader, even without a card


        public const int SCARD_PROTOCOL_T0 = 1; // - Use the T=0 protocol (value = 0x00000001)
        public const int SCARD_PROTOCOL_T1 = 2;// - Use the T=1 protocol (value = 0x00000002)
        public const int SCARD_PROTOCOL_RAW = 4;// - Use with memory type cards (value = 0x00000004)

        public const int SCARD_LEAVE_CARD = 0; // Don't do anything special on close
        public const int SCARD_RESET_CARD = 1; // Reset the card on close
        public const int SCARD_UNPOWER_CARD = 2; // Power down the card on close
        public const int SCARD_EJECT_CARD = 3; // Eject the card on close


        //取得するまでループ（デバッグ用）
        static public void ReadStart()
        {
            IntPtr context = EstablishContext();

            List<string> readersList = GetReaders(context);
            SCARD_READERSTATE[] readerStateArray = InitializeReaderState(context, readersList);

            bool quit = false;
            while (!quit)
            {
                WaitReaderStatusChange(context, readerStateArray, 1000);
                if ((readerStateArray[0].dwEventState & SCARD_STATE_PRESENT) == SCARD_STATE_PRESENT)
                {
                    string cardId = ReadCard(context, readerStateArray[0].szReader);
                    quit = true;
                }
            }
            uint ret = SCardReleaseContext(context);
        }

        // カードのIdmを取得するため一度コール
        static public string GetCardIdm()
        {
            IntPtr context = EstablishContext();

            List<string> readersList = GetReaders(context);
            SCARD_READERSTATE[] readerStateArray = InitializeReaderState(context, readersList);

            WaitReaderStatusChange(context, readerStateArray, 100);

            string idm = "";
            if ((readerStateArray[0].dwEventState & SCARD_STATE_PRESENT) == SCARD_STATE_PRESENT)
            {
                idm = ReadCard(context, readerStateArray[0].szReader);
            }
            uint ret = SCardReleaseContext(context);
            return idm;

        }

        static private IntPtr EstablishContext()
        {
            IntPtr context = IntPtr.Zero;
            uint ret = SCardEstablishContext(SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out context);

            if (ret != SCARD_S_SUCCESS)
            {
                Console.WriteLine("SCardServiceに接続できません");
                return IntPtr.Zero;

            }
            Console.WriteLine("SCardServiceに接続しました。");
            return context;
        }

        static List<string> GetReaders(IntPtr hContext)
        {
            uint pcchReaders = 0;

            uint ret = SCardListReaders(hContext, null, null, ref pcchReaders);
            if (ret != SCARD_S_SUCCESS)
            {
                return new List<string>();//リーダーの情報が取得できません。
            }

            byte[] mszReaders = new byte[pcchReaders * 2]; // 1文字2byte

            // Fill readers buffer with second call.
            ret = SCardListReaders(hContext, null, mszReaders, ref pcchReaders);
            if (ret != SCARD_S_SUCCESS)
            {
                return new List<string>();//リーダーの情報が取得できません。
            }

            UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
            string readerNameMultiString = unicodeEncoding.GetString(mszReaders);

            Console.WriteLine("ReaderName: " + readerNameMultiString);

            List<string> readersList = new List<string>();
            int nullindex = readerNameMultiString.IndexOf((char)0);   // 装置は１台のみ
            readersList.Add(readerNameMultiString.Substring(0, nullindex));
            return readersList;
        }

        static SCARD_READERSTATE[] InitializeReaderState(IntPtr hContext, List<string> readerNameList)
        {
            SCARD_READERSTATE[] readerStateArray = new SCARD_READERSTATE[readerNameList.Count];
            int i = 0;
            foreach (string readerName in readerNameList)
            {
                readerStateArray[i].dwCurrentState = SCARD_STATE_UNAWARE;
                readerStateArray[i].szReader = readerName;
                i++;
            }
            uint ret = SCardGetStatusChange(hContext, 100/*msec*/, readerStateArray, readerStateArray.Length);
            if (ret != SCARD_S_SUCCESS)
            {
                throw new ApplicationException("リーダーの初期状態の取得に失敗。code = " + ret);
            }

            return readerStateArray;
        }

        static void WaitReaderStatusChange(IntPtr hContext, SCARD_READERSTATE[] readerStateArray, int timeoutMillis)
        {
            uint ret = SCardGetStatusChange(hContext, timeoutMillis/*msec*/, readerStateArray, 1);
            switch (ret)
            {
                case SCARD_S_SUCCESS:
                    break;
                case SCARD_E_TIMEOUT:
                    Console.WriteLine("TimeOut");
                    break;
                default:
                    Console.WriteLine("リーダーの状態変化の取得に失敗。 code = " + ret);
                    break;
            }
        }

        static string ReadCard(IntPtr context, string readerName)
        {
            IntPtr hCard = Connect(context, readerName);
            string idm = ReadCardIDm(hCard);
            Console.WriteLine("Card IDm: " + idm);
            string pmm = ReadCardPMm(hCard);
            Console.WriteLine("Card PMm: " + pmm);

            Disconnect(hCard);
            return idm;
        }

        static IntPtr Connect(IntPtr hContext, string readerName)
        {
            IntPtr hCard = IntPtr.Zero;
            IntPtr activeProtocol = IntPtr.Zero;
            uint ret = SCardConnect(hContext, readerName, SCARD_SHARE_SHARED, SCARD_PROTOCOL_T1, ref hCard, ref activeProtocol);
            if (ret != SCARD_S_SUCCESS)
            {
                throw new ApplicationException("カードに接続できません。code = " + ret);
            }
            return hCard;
        }

        static string ReadCardIDm(IntPtr hCard)
        {
            byte maxRecvDataLen = 64;
            byte[] recvBuffer = new byte[maxRecvDataLen + 2];
            byte[] sendBuffer = new byte[] { 0xff, 0xca, 0x00, 0x00, maxRecvDataLen };
            int recvLength = Transmit(hCard, sendBuffer, recvBuffer);

            string cardId = BitConverter.ToString(recvBuffer, 0, recvLength - 2).Replace("-", "");
            return cardId;
        }

        static string ReadCardPMm(IntPtr hCard)
        {
            byte maxRecvDataLen = 64;
            byte[] recvBuffer = new byte[maxRecvDataLen + 2];
            byte[] sendBuffer = new byte[] { 0xff, 0xca, 0x01, 0x00, maxRecvDataLen };
            int recvLength = Transmit(hCard, sendBuffer, recvBuffer);

            string cardId = BitConverter.ToString(recvBuffer, 0, recvLength - 2).Replace("-", "");
            return cardId;
        }

        static int Transmit(IntPtr hCard, byte[] sendBuffer, byte[] recvBuffer)
        {
            SCARD_IO_REQUEST ioRecv = new SCARD_IO_REQUEST
            {
                cbPciLength = 255
            };

            int pcbRecvLength = recvBuffer.Length;
            int cbSendLength = sendBuffer.Length;
            IntPtr SCARD_PCI_T1 = GetPciT1();
            uint ret = SCardTransmit(hCard, SCARD_PCI_T1, sendBuffer, cbSendLength, ioRecv, recvBuffer, ref pcbRecvLength);
            if (ret != SCARD_S_SUCCESS)
            {
                throw new ApplicationException("カードへの送信に失敗しました。code = " + ret);
            }
            return pcbRecvLength; // 受信したバイト数(recvBufferに受け取ったバイト数)
        }

        static private IntPtr GetPciT1()
        {
            IntPtr handle = LoadLibrary("Winscard.dll");
            IntPtr pci = GetProcAddress(handle, "g_rgSCardT1Pci");
            FreeLibrary(handle);
            return pci;
        }

        static void Disconnect(IntPtr hCard)
        {
            uint ret = SCardDisconnect(hCard, SCARD_LEAVE_CARD);
            if (ret != SCARD_S_SUCCESS)
            {
                throw new ApplicationException("カードとの接続を切断できません。code = " + ret);
            }
        }
    }
}