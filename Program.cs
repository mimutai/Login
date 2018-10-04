using System;

namespace Login
{
    class Program
    {
        static void Main(string[] args)
        {
            //NFC_Reader.GetCardIdm();

            string filePath = "C:\\Users\\mimutai\\Documents\\Visual Studio Code\\Login\\Account.json";
            Account.GetIDDictionary(filePath);
        }
    }
}