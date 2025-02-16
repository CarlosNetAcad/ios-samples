// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using MusicKitSample.Controllers;
using MusicKitSample.Models;
using CoreGraphics;
using System.Threading.Tasks;
using StoreKit;

namespace MusicKitSample {
	public partial class RecentlyPlayedTableViewController : UITableViewController, IMediaSearchTableViewCellDelegate {
		#region Fields

		static readonly object padlock = new object ();

		ImageCacheManager imageCacheManager;
		MediaItem [] mediaItems;

		#endregion

		#region Properties

		public AuthorizationManager AuthorizationManager { get; set; }
		public AppleMusicManager AppleMusicManager { get; set; }
		public MusicPlayerManager MusicPlayerManager { get; set; }
		public MediaLibraryManager MediaLibraryManager { get; set; }

		#endregion

		#region Constructors

		public RecentlyPlayedTableViewController (IntPtr handle) : base (handle)
		{
		}

		#endregion

		#region Controller Life Cycle

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			imageCacheManager = new ImageCacheManager ();
			mediaItems = new MediaItem [0];
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);

			if (AppleMusicManager.FetchDeveloperToken () == null) {
				var alertController = UIAlertController.Create ("Error",
										"No Developer Token was specified. See the README for more information.",
										UIAlertControllerStyle.Alert);
				alertController.AddAction (UIAlertAction.Create ("Dismiss", UIAlertActionStyle.Cancel, null));
				PresentViewController (alertController, true, null);
			} else if (AuthorizationManager.UserToken == string.Empty) {
				var alertController = UIAlertController.Create ("Error",
										"No User Token was specified. Request Authorization using the \"Authorization\" tab.",
										UIAlertControllerStyle.Alert);
				alertController.AddAction (UIAlertAction.Create ("Dismiss", UIAlertActionStyle.Cancel, null));
				PresentViewController (alertController, true, null);
			} else {
				Task.Factory.StartNew (async () => await RefreshData ());
			}
		}

		#endregion

		#region TableView Source

		public override nint NumberOfSections (UITableView tableView) => 1;

		public override nint RowsInSection (UITableView tableView, nint section) => mediaItems.Length;

		public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
		{
			var cell = tableView.DequeueReusableCell (MediaItemTableViewCell.Key, indexPath) as MediaItemTableViewCell;
			var mediaItem = mediaItems [indexPath.Row];

			cell.MediaItem = mediaItem;
			cell.Delegate = this;

			// Image loading.
			var imageUrl = mediaItem.Artwork.GenerateImageUrl (new CGSize (90, 90));

			if (imageCacheManager.GetCachedImage (imageUrl) is UIImage image)
				// Cached: set immediately.
				cell.AssetCoverArt = image;
			else {
				// Not cached, so load then fade it in.
				Task.Factory.StartNew (async () => {
					image = await imageCacheManager.FetchImage (imageUrl);

					// Check the cell hasn't recycled while loading.
					if (cell.MediaItem.Id == mediaItem.Id)
						InvokeOnMainThread (() => cell.AssetCoverArt = image);
				});
			}

			var cloudServceCapabilities = AuthorizationManager.CloudServiceCapabilities;

			/* It is important to actually check if your application has the 
			 * appropriate `SKCloudServiceCapability` options before enabling 
			 * functionality related to playing back content from the Apple 
			 * Music Catalog or adding items to the user's Cloud Music Library.
			 */
			cell.AddToPlaylistButtonEnabled = cloudServceCapabilities.HasFlag (SKCloudServiceCapability.AddToCloudMusicLibrary);
			cell.PlayItemButtonEnabled = cloudServceCapabilities.HasFlag (SKCloudServiceCapability.MusicCatalogPlayback);

			return cell;
		}

		#endregion

		#region Notification Observer Callback Methods.

		async Task RefreshData ()
		{
			// Your application should handle these errors appropriately depending on the kind of error.
			var items = new MediaItem [0];
			try {
				items = await AppleMusicManager.PerformAppleMusicGetRecentlyPlayedAsync (AuthorizationManager.UserToken);
			} catch (NSErrorException ex) {
				var underlyingError = ex.Error.UserInfo [NSError.UnderlyingErrorKey] as NSError;
				var message = underlyingError?.LocalizedDescription ?? "Encountered unexpected error.";

				var alertController = UIAlertController.Create ("Error", message, UIAlertControllerStyle.Alert);
				alertController.AddAction (UIAlertAction.Create ("Dismiss", UIAlertActionStyle.Cancel, null));

				InvokeOnMainThread (() => PresentViewController (alertController, true, null));
			} finally {
				lock (padlock) {
					mediaItems = items;
				}
				InvokeOnMainThread (() => TableView.ReloadData ());
			}
		}

		#endregion

		#region MediaItemTableViewCell Delegate

		public void AddToPlaylist (MediaItemTableViewCell mediaSearchTableViewCell, MediaItem mediaItem) =>
			Task.Factory.StartNew (async () => await MediaLibraryManager.AddItemAsync (mediaItem.Id));

		public void PlayMediaItem (MediaItemTableViewCell mediaSearchTableViewCell, MediaItem mediaItem) =>
			MusicPlayerManager.BeginPlayback (mediaItem.Id);

		#endregion
	}
}
