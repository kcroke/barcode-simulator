﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;


namespace BarcodeSimulator
{

    public partial class BarcodeSimulatorControl : Form
    {

        public BarcodeSimulatorControl()
        {
            InitializeComponent();
        }

        private void BarcodeSimulatorControl_Load(object sender, EventArgs e)
        {
            // setup a new Hotkey to watch 
            Hotkey hk = GetUnusedHotKey();

            // show the hotkey on the form as an FYI
            hotkeyTextBox.Text = hk.ToString();

            // list all available keys in the 'ends with' drop down
            Enum.GetNames(typeof(Keys)).ToList().ForEach(k => endsWithComboBox.Items.Add(k));

            // focus on the text box for adding new string
            ActiveControl = newStringTextBox;

            SetupToolTips();
        }

        private Hotkey GetUnusedHotKey()
        {
            // Starting at Z and going down to A, find a free hotkey and register it.
            var zToA = EnumHelper.GetAllItems<Keys>()
                .Where(key => key.ToString().Length == 1 && char.IsLetter(key.ToString()[0]))
                .OrderByDescending(key => key.ToString()[0]);

            Hotkey hk = new Hotkey {Windows = true};
            foreach (var key in zToA)
            {
                hk.KeyCode = key;
                if (hk.Register(this))
                {
                    break;
                }
            }
            hk.Pressed += HotkeyPressed;
            return hk;
        }

        private void SetupToolTips()
        {
            var tt = new ToolTip {InitialDelay = 500, ReshowDelay = 500, ShowAlways = true};
            
            tt.SetToolTip(delayNumeric, "Delay in milliseconds between each keypress when sending a barcode.");
            tt.SetToolTip(hotkeyTextBox, "Activation key sequence. Press " + hotkeyTextBox.Text + " to send the next barcode.");
            tt.SetToolTip(endsWithComboBox, "Optionally ends each barcode sending with this key.");
            tt.SetToolTip(newStringTextBox, "Enter a series of characters you want to simulate. Press Enter to add it.");
            tt.SetToolTip(itemsListView, "List of barcodes to send. Sends in order round-robin style. Select one and press Delete to remove it.");
        }

        private async void HotkeyPressed(object sender, HandledEventArgs e)
        {
            Debug.WriteLine("Hotkey pressed");

            while (IsAnyKeyPressed())
            {
                // Made this async because sleep hogs the UI thread and the calls to check the keyboard never changed
                // even though the key was up
                await Task.Delay(10);
            }

            if (itemsListView.Items.Count == 0)
            {
                MessageBox.Show("You have to add some strings to the list first.", "Fail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var endKey = Keys.None;

            if (endsWithComboBox.SelectedIndex > 0)
                Enum.TryParse(endsWithComboBox.SelectedItem.ToString(), out endKey);

            var s = GetNextCode();

            // do the delayed key sending in a separate thread so we don't hang the window
            ThreadStart starter = () => StartSending(s, (int) delayNumeric.Value, endKey);
            var t = new Thread(starter) { Name = "Sending keys " + s };
            t.Start();
        }

        private bool IsAnyKeyPressed()
        {
            List<Key> allPossibleKeys = EnumHelper.GetAllItems<Key>().Where(key => key != Key.None).ToList();
            var pressedKeys = allPossibleKeys.Where(Keyboard.IsKeyDown).ToList();
            Debug.WriteLine(string.Format("Pressed Keys: {0}", string.Join(",", pressedKeys)));

            return pressedKeys.Any();
        }

        private static void StartSending(string text, int delay, Keys endKey = Keys.None)
        {

            foreach (var s in text.Select(character => character.ToString()))
            {
                Debug.WriteLine("{0} Sending text '{1}'", DateTime.Now.ToString("HH:mm:ss.fff"), s);
                SendKeys.SendWait(s);
                SendKeys.Flush();
                Thread.Sleep(delay);
            }

            // if configured, send an 'end' key to signal that we're at the end of the barcode
            if (endKey != Keys.None)
                SendKeys.SendWait("{" + Enum.GetName(typeof (Keys), endKey) + "}");

            // beep!
            System.Media.SystemSounds.Beep.Play();
        }

        /// <summary>
        /// Get the next value from the list of items and advance the selection
        /// If we're at the end of the list, go back to the beginning
        /// </summary>
        /// <returns></returns>
        private string GetNextCode()
        {
            if (itemsListView.SelectedItems.Count == 0)
            {
                itemsListView.Items[0].Selected = true;
                itemsListView.Select();
            }

            var currentIndex = itemsListView.SelectedItems[0].Index;

            var s = itemsListView.Items[currentIndex].Text;

            if (currentIndex == itemsListView.Items.Count - 1)
            {
                itemsListView.Items[currentIndex].Selected = false;
                itemsListView.Items[0].Selected = true;
            }
            else
                itemsListView.Items[currentIndex + 1].Selected = true;

            itemsListView.Select();

            return s;
        }

        /// <summary>
        /// Ignore all key entry aside from Enter. Enter adds the value to the
        /// string list and clears the new string entry text box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newStringTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            var code = newStringTextBox.Text;

            
            itemsListView.Items.Add(new ListViewItem(new[] { code, Barcode.GetTypeName(code) }));
            itemsListView.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            
            newStringTextBox.Clear();
        }

        /// <summary>
        /// Handle deleting items from the string list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void itemsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete)
                return;

            if (itemsListView.SelectedItems.Count == 0)
                return;

            itemsListView.Items.Remove(itemsListView.SelectedItems[0]);
        }

        private void newStringTextBox_TextChanged(object sender, EventArgs e)
        {
            var text = newStringTextBox.Text;

            if (string.IsNullOrEmpty(text))
            {
                newCodeTypeLabel.Text = null;
                return;
            }

            var type = Barcode.GetTypeName(text);

            if (newCodeTypeLabel.Text == type)
                return;

            newCodeTypeLabel.Text = type;
        }
    }
}
