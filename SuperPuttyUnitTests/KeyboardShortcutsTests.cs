using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Windows.Forms;
using SuperPutty.Gui;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPuttyUnitTests
{
    [TestFixture]
    public class KeyboardShortcutsTets
    {
        [Test]
        public void KeysToStringTest()
        {
            String s = Keys.None.ToString();
            ClassicAssert.AreEqual(s, "None");

            Keys k = Keys.Control;
            ClassicAssert.AreEqual("Control", k.ToString());
            k |= Keys.M;
            ClassicAssert.AreEqual("M, Control", k.ToString());

            k |= Keys.Shift;
            ClassicAssert.AreEqual("M, Shift, Control", k.ToString());

            Keys k2 = (Keys) Enum.Parse(typeof(Keys), k.ToString());
            ClassicAssert.AreEqual(k, k2);
        }

        [Test]
        public void ToStringTest()
        {
            KeyboardShortcut ks = new KeyboardShortcut();

            ClassicAssert.AreEqual("", ks.ShortcutString);

            ks.Key = Keys.F11;
            ClassicAssert.AreEqual("F11", ks.ShortcutString);

            ks.Key = Keys.PageUp;
            ks.Modifiers = Keys.Control;
            ClassicAssert.AreEqual("Ctrl+PageUp", ks.ShortcutString);

            ks.Modifiers |= Keys.Shift;
            ClassicAssert.AreEqual("Ctrl+Shift+PageUp", ks.ShortcutString);

        }

        [Test]
        public void getcommandTest()
        {
            String command = CommandLineOptions.getcommand("-pw 12sa12 -we aasd", "-pw");
            ClassicAssert.AreEqual("12sa12", command);

            command = CommandLineOptions.getcommand(" -pw 12sa12 -we aasd", "-pw");
            ClassicAssert.AreEqual("12sa12", command);

            command = CommandLineOptions.getcommand("-pw \"12sa12\" -we aasd", "-pw");
            ClassicAssert.AreEqual("12sa12", command);

            command = CommandLineOptions.getcommand(" -pw  -pw \"12sa12\" -we aasd", "-pw");
            ClassicAssert.AreEqual("12sa12", command);


            command = CommandLineOptions.getcommand("-pw \"12sa12\" -we aasd", "-pw");
            ClassicAssert.AreEqual("12sa12", command);

            command = CommandLineOptions.getcommand("  -pw  \"12sa12\" -we aasd", "-pw");
            ClassicAssert.AreEqual("", command);

            command = CommandLineOptions.getcommand("  -pw: \"12sa12\" -we aasd", "-pw");
            ClassicAssert.AreEqual("", command);

            command = CommandLineOptions.getcommand(" -pw  -pw \"12sa12 -we aasd", "-pw");
            ClassicAssert.AreEqual("", command);

            command = CommandLineOptions.getcommand(" -pw  -pw \"12sa12 -we aasd\"", "-pw");
            ClassicAssert.AreEqual("12sa12 -we aasd", command);

            command = CommandLineOptions.getcommand(@" -pw  -pw \+**jioi12sa12'k*+/\ -we aasd'""", "-pw");
            ClassicAssert.AreEqual(@"\+**jioi12sa12'k*+/\", command);

        }

        [TestView]
        public void DialogBasicTest()
        {
            KeyboardShortcutEditor form = new KeyboardShortcutEditor();
            form.ShowDialog(null, new KeyboardShortcut { Name = "test", Key = Keys.A, Modifiers = Keys.Control });
        }




    }
}
