using System;

namespace Octgn.Communication.Utility
{
    public class UID
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwyxzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string Generate(int id) {
            var rng = new Random(id);

            var chars = new char[8];
            for (var i = 0; i < 8; i++) {
                chars[i] = Alphabet[rng.Next(Alphabet.Length)];
            }
            return new string(chars);
        }
    }
}