using System.Windows.Forms;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    /// <summary>Live input: which key presses are repeated in the other terminals, and how</summary>
    [TestFixture]
    public class LiveInputTests
    {
        /// <summary>A fake keyboard layout: letters, shift for upper case, AltGr+Q = @</summary>
        private static string Chars(Keys key, uint scan, bool shift, bool altGr)
        {
            if (altGr)
            {
                return key == Keys.Q ? "@" : null;
            }
            if (key >= Keys.A && key <= Keys.Z)
            {
                string c = ((char)('a' + (key - Keys.A))).ToString();
                return shift ? c.ToUpperInvariant() : c;
            }
            return key == Keys.Oem3 ? null : " "; // Oem3 = dead key
        }

        private static Keystroke? Translate(Keys key, bool shift = false, bool control = false, bool alt = false)
        {
            return LiveInput.Translate(key, 0x1c, false, shift, control, alt, Chars);
        }

        [Test]
        public void CharactersUseTheLayout()
        {
            ClassicAssert.AreEqual("l", Translate(Keys.L).Value.Chars);
            ClassicAssert.AreEqual("L", Translate(Keys.L, shift: true).Value.Chars);
            ClassicAssert.AreEqual("@", Translate(Keys.Q, control: true, alt: true).Value.Chars, "AltGr is not Ctrl+Alt");
            ClassicAssert.IsNull(Translate(Keys.Oem3), "dead keys are skipped");
        }

        [Test]
        public void ControlLetterIsControlCharacter()
        {
            ClassicAssert.AreEqual("\u0003", Translate(Keys.C, control: true).Value.Chars);
            ClassicAssert.AreEqual("\u0004", Translate(Keys.D, control: true).Value.Chars);
            ClassicAssert.IsNull(Translate(Keys.V, shift: true, control: true), "Ctrl+Shift shortcuts stay local");
        }

        [Test]
        public void AltKeyIsEscapePrefix()
        {
            ClassicAssert.AreEqual("\u001bb", Translate(Keys.B, alt: true).Value.Chars);
        }

        [Test]
        public void TerminalKeysGoAsKeys()
        {
            Keystroke enter = Translate(Keys.Enter).Value;
            ClassicAssert.IsNull(enter.Chars);
            ClassicAssert.AreEqual(Keys.Enter, enter.Key);
            ClassicAssert.AreEqual(0x1cu, enter.ScanCode);
            ClassicAssert.AreEqual(Keys.Up, Translate(Keys.Up).Value.Key);
            ClassicAssert.AreEqual(Keys.F5, Translate(Keys.F5).Value.Key);
        }

        [Test]
        public void ShortcutsAndModifiersStayLocal()
        {
            ClassicAssert.IsNull(Translate(Keys.Tab, control: true), "Ctrl+Tab switches tabs");
            ClassicAssert.IsNull(Translate(Keys.F4, alt: true));
            ClassicAssert.IsNull(Translate(Keys.LShiftKey, shift: true));
            ClassicAssert.IsNull(Translate(Keys.ControlKey, control: true));
        }
    }
}
