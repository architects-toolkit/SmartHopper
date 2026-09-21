/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Grasshopper;
using Rhino.UI;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Presents a floating (non-modal) checklist for a staged canvas proposal while the
    /// proposal is painted over the actual canvas. The canvas stays fully interactive so
    /// the user can pan and zoom to inspect the highlighted changes.
    /// </summary>
    public sealed class CanvasChangeReviewDialog : Form
    {
        private readonly Dictionary<string, CheckBox> checkBoxes = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        private readonly Label summaryLabel;
        private readonly CanvasChangeReviewSession session;

        private CanvasChangeReviewDialog(CanvasChangeReviewSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.Title = session.Title;
            this.Resizable = true;

            // Modeless dialog that must stay visible while the user pans and zooms the
            // Grasshopper canvas to inspect the staged changes.
            this.Topmost = true;
            this.ClientSize = new Size(440, 620);
            this.MinimumSize = new Size(380, 420);
            this.Padding = new Padding(16);

            var title = new Label
            {
                Text = "Review AI canvas changes",
                Font = new Font(SystemFont.Bold, 16),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var zoomButton = new Button
            {
                Text = "Zoom to changes",
                ToolTip = "Pan and zoom the canvas so all staged changes are visible",
            };
            zoomButton.Click += (_, _) => CanvasChangePreviewOverlay.FrameChanges(Instances.ActiveCanvas, this.session);
            var subtitle = new Label
            {
                Text = $"{session.Items.Count} staged change(s) from {session.Source}. The canvas is unchanged until you apply.",
                Wrap = WrapMode.Word,
                TextColor = Colors.Gray,
            };

            var changesLayout = new DynamicLayout
            {
                DefaultSpacing = new Size(8, 5),
                Padding = new Padding(0, 8),
            };
            foreach (var item in session.Items)
            {
                changesLayout.Add(this.CreateChangeRow(item));
            }

            var scrollable = new Scrollable
            {
                Border = BorderType.None,
                Content = changesLayout,
                ExpandContentWidth = true,
            };
            this.summaryLabel = new Label
            {
                TextColor = Colors.Gray,
            };

            var acceptAllButton = new Button { Text = "Accept all" };
            acceptAllButton.Click += (_, _) => this.SetAll(true);
            var rejectAllButton = new Button { Text = "Reject all" };
            rejectAllButton.Click += (_, _) => this.SetAll(false);
            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Click += (_, _) => this.Close();
            var applyButton = new Button { Text = "Apply selected" };
            applyButton.Click += (_, _) =>
            {
                this.ApplyRequested = true;
                this.Close();
            };

            var selectionButtons = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Items = { acceptAllButton, rejectAllButton },
            };
            var actionButtons = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Items = { cancelButton, applyButton },
            };

            this.Content = new TableLayout
            {
                Spacing = new Size(8, 10),
                Rows =
                {
                    new TableRow(new TableLayout
                    {
                        Spacing = new Size(8, 0),
                        Rows =
                        {
                            new TableRow(
                                new TableCell(title, true),
                                new TableCell(zoomButton, false)),
                        },
                    }),
                    new TableRow(subtitle),
                    new TableRow(scrollable) { ScaleHeight = true },
                    new TableRow(this.summaryLabel),
                    new TableRow(new TableLayout
                    {
                        Spacing = new Size(8, 0),
                        Rows =
                        {
                            new TableRow(
                                new TableCell(selectionButtons, true),
                                new TableCell(actionButtons, false)),
                        },
                    }),
                },
            };

            this.session.SelectionChanged += this.OnSelectionChanged;
            this.UpdateSummary();
        }

        /// <summary>Gets a value indicating whether the user chose to apply the selected changes.</summary>
        public bool ApplyRequested { get; private set; }

        /// <summary>
        /// Shows a staged review as a floating non-modal window so the canvas stays
        /// interactive, and completes when the user closes it.
        /// </summary>
        /// <param name="session">Review session.</param>
        /// <param name="cancellationToken">Cancels the review and closes the dialog.</param>
        /// <returns><c>true</c> when the selected changes should be applied.</returns>
        public static Task<bool> ShowReviewAsync(CanvasChangeReviewSession session, CancellationToken cancellationToken = default)
        {
            var dialog = new CanvasChangeReviewDialog(session);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() =>
                Application.Instance?.AsyncInvoke(() =>
                {
                    try
                    {
                        dialog.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                        // The dialog was already closed and disposed.
                    }
                }));
            dialog.Closed += (_, _) =>
            {
                CanvasChangePreviewOverlay.End(session);
                var result = dialog.ApplyRequested;
                dialog.Dispose();
                completion.TrySetResult(result);
            };

            CanvasChangePreviewOverlay.Begin(session);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mainWindow = RhinoEtoApp.MainWindow;
                if (mainWindow != null)
                {
                    dialog.Owner = mainWindow;
                    dialog.ShowInTaskbar = false;
                }

                dialog.Show();
            }
            catch
            {
                CanvasChangePreviewOverlay.End(session);
                dialog.Dispose();
                throw;
            }

            return completion.Task;
        }

        /// <inheritdoc/>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var referenceWindow = RhinoEtoApp.MainWindow ?? this.Owner;
            var screen = referenceWindow != null
                ? Screen.FromRectangle(referenceWindow.Bounds)
                : Screen.PrimaryScreen;
            if (screen != null)
            {
                var workArea = screen.WorkingArea;
                this.Location = new Point(
                    (int)(workArea.Right - this.Width - 24),
                    (int)(workArea.Top + Math.Max(24, (workArea.Height - this.Height) / 2)));
            }

            CanvasChangePreviewOverlay.FrameChanges(Instances.ActiveCanvas, this.session, onlyWhenNotFullyVisible: true);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.session.SelectionChanged -= this.OnSelectionChanged;
            }

            base.Dispose(disposing);
        }

        private Control CreateChangeRow(CanvasChangeReviewItem item)
        {
            var checkBox = new CheckBox
            {
                Checked = item.IsAccepted,
            };
            checkBox.CheckedChanged += (_, _) => this.session.SetAccepted(item.Key, checkBox.Checked == true);
            this.checkBoxes[item.Key] = checkBox;

            var kindLabel = new Label
            {
                Text = GetKindLabel(item.Kind),
                TextColor = GetKindColor(item.Kind),
                Font = new Font(SystemFont.Bold, 9),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 62,
            };
            var titleLabel = new Label
            {
                Text = item.Title,
                Font = new Font(SystemFont.Bold, 10),
                Wrap = WrapMode.Word,
            };
            var detailLabel = new Label
            {
                Text = item.Detail,
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 9),
                Wrap = WrapMode.Word,
            };
            var textLayout = new DynamicLayout
            {
                DefaultSpacing = new Size(0, 2),
            };
            textLayout.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(item.Detail))
            {
                textLayout.Add(detailLabel);
            }

            var row = new Panel
            {
                Padding = new Padding(7),
                ToolTip = "Double-click to zoom to this change",
                Content = new TableLayout
                {
                    Spacing = new Size(8, 0),
                    Rows =
                    {
                        new TableRow(
                            new TableCell(checkBox, false),
                            new TableCell(kindLabel, false),
                            new TableCell(textLayout, true)),
                    },
                },
            };
            row.MouseEnter += (_, _) => CanvasChangePreviewOverlay.Highlight(item.Key);
            row.MouseLeave += (_, _) => CanvasChangePreviewOverlay.Highlight(null);
            row.MouseDoubleClick += (_, _) => CanvasChangePreviewOverlay.FrameItem(Instances.ActiveCanvas, this.session, item);
            return row;
        }

        private void SetAll(bool isAccepted)
        {
            this.session.SetAllAccepted(isAccepted);
            foreach (var checkBox in this.checkBoxes.Values)
            {
                checkBox.Checked = isAccepted;
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            this.UpdateSummary();
        }

        private void UpdateSummary()
        {
            this.summaryLabel.Text = $"{this.session.AcceptedCount} of {this.session.Items.Count} changes selected. Applied changes are recorded in Grasshopper's undo history.";
        }

        private static string GetKindLabel(CanvasChangeKind kind)
        {
            return kind switch
            {
                CanvasChangeKind.ComponentAdded => "ADD",
                CanvasChangeKind.ComponentModified => "MODIFY",
                CanvasChangeKind.ComponentRemoved => "REMOVE",
                CanvasChangeKind.ConnectionAdded => "WIRE +",
                CanvasChangeKind.ConnectionRemoved => "WIRE −",
                CanvasChangeKind.GroupAdded => "GROUP +",
                CanvasChangeKind.GroupModified => "GROUP",
                CanvasChangeKind.GroupRemoved => "GROUP −",
                _ => "CHANGE",
            };
        }

        private static Color GetKindColor(CanvasChangeKind kind)
        {
            return kind switch
            {
                CanvasChangeKind.ComponentAdded => Color.FromArgb(45, 164, 78),
                CanvasChangeKind.ComponentModified => Color.FromArgb(191, 135, 0),
                CanvasChangeKind.ComponentRemoved => Color.FromArgb(207, 34, 46),
                CanvasChangeKind.ConnectionAdded or CanvasChangeKind.ConnectionRemoved => Color.FromArgb(9, 105, 218),
                _ => Color.FromArgb(130, 80, 223),
            };
        }
    }
}
