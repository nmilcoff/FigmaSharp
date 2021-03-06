﻿/* 
 * FigmaRuntime.cs 
 * 
 * Author:
 *   Jose Medrano <josmed@microsoft.com>
 *
 * Copyright (C) 2018 Microsoft, Corp
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit
 * persons to whom the Software is furnished to do so, subject to the
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN
 * NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
using System;
using Gtk;
using MonoDevelop.Core;

namespace MonoDevelop.Figma
{
    class FigmaOptionsWidget : VBox
    {
        Entry tokenEntry;
        FigmaOptionsPanel panel;

        public FigmaOptionsWidget(FigmaOptionsPanel figmaOptionsPanel)
        {
            panel = figmaOptionsPanel;


            var tokenLayout = new HBox();
            var tokenTip = new Label("Get your token from the Figma app:\n" +
                "Menu → Help and Account → Personal Access Tokens");

            tokenTip.Sensitive = false;
            tokenTip.Xalign = 0;
            tokenTip.Xpad = 148;

            var tokenLabel = new Label();
            tokenLabel.Text = GettextCatalog.GetString("Personal Access Token:");

            tokenEntry = new Entry(FigmaRuntime.Token)
            {
                Visibility = false,
                WidthRequest = 400
            };

            tokenEntry.Changed += NeedsStoreValue;
            tokenEntry.FocusOutEvent += NeedsStoreValue;

            tokenLayout.PackStart(tokenLabel, false, false, 0);
            tokenLayout.PackStart(tokenEntry, false, false, 6);


            var convertersLayout = new HBox();

            reloadButton = new Button() { Label = "Reload Converters" };
            reloadButton.Activated += RefresButton_Activated;

            convertersLayout.PackStart(reloadButton, false, false, 6);


            PackStart(tokenLayout, false, false, 0);
            PackStart(tokenTip, false, false, 6);
            PackStart(new Label(""), true, true, 0);
            PackStart(new Label("<b>Debugging</b>") { UseMarkup = true, Xalign = 0 }, false, false, 0);
            PackStart(convertersLayout, false, false, 6);

            ShowAll();
        }

        Button reloadButton;

        private void RefresButton_Activated(object sender, EventArgs e)
        {

        }

        void NeedsStoreValue(object sender, EventArgs e)
        {
            FigmaRuntime.Token = tokenEntry.Text;
        }

        internal void ApplyChanges()
        {
            FigmaRuntime.Token = tokenEntry.Text;
        }

        public override void Dispose()
        {
            reloadButton.Activated -= RefresButton_Activated;
        }
    }
}
