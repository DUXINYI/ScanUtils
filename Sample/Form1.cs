using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScanUtils;
namespace Sample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1)
                return;
            if(ScanUtils.ScanUtils.CheckScanner(listView1.SelectedItems[0].Text))
            {
                MessageBox.Show("设备可用");
            }
            else
            {
                MessageBox.Show("设备不可用");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1)
                return;
            ScanUtils.ScanUtils.StartScan(listView1.SelectedItems[0].Text, "./scan").ContinueWith(task =>
             {
                 MessageBox.Show(string.Join(",", task.Result.ToList()));
             });
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var drivers = ScanUtils.ScanUtils.DicoverDrivers();
            listView1.Items.AddRange(drivers.Select(t => new ListViewItem()
            {
                  Text = t.Name,
                   
            }).ToArray());
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ScanUtils.ScanUtils.StopScan();
        }
    }
}
