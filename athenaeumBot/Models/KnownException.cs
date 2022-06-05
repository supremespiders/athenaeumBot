using System;

namespace athenaeumBot.Models
{
    public class KnownException : Exception
    {
        public KnownException(string s) : base(s)
        {

        }
    }
}