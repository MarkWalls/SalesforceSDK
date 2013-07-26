using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using Salesforce;
using Xamarin.Auth;
using System.Linq;
using MonoTouch.CoreAnimation;
using System.Json;

namespace SalesforceSample.iOS
{
	public partial class RootViewController : UITableViewController
	{
		DataSource dataSource;

		UIBarButtonItem ActivityItem {
			get;
			set;
		}

		public RootViewController () : base ("RootViewController", null)
		{
			Title = NSBundle.MainBundle.LocalizedString ("Master", "Master");

			var item = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
			item.Hidden = false;
			item.StartAnimating();

			ActivityItem = new UIBarButtonItem (item);

			// Custom initialization
		}

		public DetailViewController DetailViewController {
			get;
			set;
		}

		public UIViewController LoginController { get; protected set; }

		void AddNewItem (object sender, EventArgs args)
		{
			try {
				LoginController = Client.GetLoginInterface () as UIViewController;
				PresentViewController(LoginController, true, null);
			} catch (Exception ex) {
				Console.WriteLine (ex.Message);
			}
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		SalesforceClient Client;
		ISalesforceUser Account { get; set; }

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			// Perform any additional setup after loading the view, typically from a nib.
			NavigationItem.RightBarButtonItem = EditButtonItem;

			var addButton = new UIBarButtonItem (UIBarButtonSystemItem.Add, AddNewItem);
			NavigationItem.LeftBarButtonItem = addButton;

			TableView.Source = dataSource = new DataSource (this);

			var key = "3MVG9A2kN3Bn17hueOTBLV6amupuqyVHycNQ43Q4pIHuDhYcP0gUA0zxwtLPCcnDlOKy0gopxQ4dA6BcNWLab";

			var redirectUrl = new Uri("com.sample.salesforce:/oauth2Callback"); // TODO: Move oauth redirect to constant or config

			Client = new SalesforceClient (key, redirectUrl);

			Client.AuthRequestCompleted += (sender, e) => {
				if (e.IsAuthenticated){
					// TODO: Transition to regular application UI.
					Console.WriteLine("Auth success: " + e.Account.Username);
				}

				DismissViewController(true, new NSAction(
					()=>
					{
						NavigationItem.RightBarButtonItem = null;
						ShowLoadingState ();
						LoadAccounts ();

					}));

				Account = e.Account;
				Client.Save(Account);
			};

			var users = Client.LoadUsers ();
			if (users.Count () == 0)
			{
				var loginController = Client.GetLoginInterface () as UIViewController;
				PresentViewController (loginController, true, null);
			} 
			else
			{
				ShowLoadingState ();
				LoadAccounts ();
			}
		}

		void LoadAccounts ()
		{
			Console.WriteLine (Client.CurrentUser);

			var request = new ReadRequest {
//				Resource = new Search { QueryText = "FIND {John}" }
				Resource = new Query { Statement = "SELECT Id, Name, AccountNumber FROM Account" }
			};

			var response = Client.Process<ReadRequest> (request);
			var result = response.GetResponseText ();

			var results = System.Json.JsonValue.Parse(result)["records"];

			foreach(var r in results)
			{
				Console.WriteLine (r);
			}

			dataSource.Objects = results.OfType<object>().ToList();
		}

		public void ShowLoadingState()
		{
			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = true;
		}

		public void HideLoadingState()
		{
			UIApplication.SharedApplication.NetworkActivityIndicatorVisible = false;
		}

		class DataSource : UITableViewSource
		{
			static readonly NSString CellIdentifier = new NSString ("DataSourceCell");
			List<object> objects = new List<object> ();
			RootViewController controller;

			public DataSource (RootViewController controller)
			{
				this.controller = controller;
			}

			public List<object> Objects {
				get { return objects; }
				set { objects = value; this.controller.TableView.ReloadData ();}
			}
			// Customize the number of sections in the table view.
			public override int NumberOfSections (UITableView tableView)
			{
				return 1;
			}

			public override int RowsInSection (UITableView tableview, int section)
			{
				return objects.Count;
			}

			// Customize the appearance of table view cells.
			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
				var cell = tableView.DequeueReusableCell (CellIdentifier);
				if (cell == null) {
					cell = new UITableViewCell (UITableViewCellStyle.Subtitle, CellIdentifier);
					cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
				}

				var o = (JsonObject)objects [indexPath.Row];
				cell.TextLabel.Text = o["Name"];
				cell.DetailTextLabel.Text = o["AccountNumber"];
				return cell;
			}

			public override bool CanEditRow (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
			{
				// Return false if you do not want the specified item to be editable.
				return true;
			}

			public async override void CommitEditingStyle (UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
			{
				if (editingStyle == UITableViewCellEditingStyle.Delete) {

					var selected = controller.dataSource.Objects.ElementAtOrDefault (indexPath.Row) as JsonValue;
					var selectedObject = new SObject (selected as JsonObject);
					// Delete the row from the data source.
					var request = new DeleteRequest (selectedObject);
					request.Resource = selectedObject;

					await controller.Client.ProcessAsync (request);
					((DataSource)tableView.Source).Objects.Remove (selectedObject);
					tableView.ReloadData ();

				} else if (editingStyle == UITableViewCellEditingStyle.Insert) {
					// Create a new instance of the appropriate class, insert it into the array, and add a new row to the table view.
				}
			}
			/*
			// Override to support rearranging the table view.
			public override void MoveRow (UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
			{
			}
			*/
			/*
			// Override to support conditional rearranging of the table view.
			public override bool CanMoveRow (UITableView tableView, NSIndexPath indexPath)
			{
				// Return false if you do not want the item to be re-orderable.
				return true;
			}
			*/
			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
				if (controller.DetailViewController == null)
					controller.DetailViewController = new DetailViewController ();

				controller.DetailViewController.SetDetailItem (objects [indexPath.Row]);

				// Pass the selected object to the new view controller.
				controller.NavigationController.PushViewController (controller.DetailViewController, true);
			}
		}
	}
}