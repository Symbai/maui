using System;
using System.ComponentModel;
using ElmSharp;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls.Compatibility.Platform.Tizen.Native;
using Microsoft.Maui.Devices;
using EContainer = ElmSharp.Container;
using ERect = ElmSharp.Rect;
using NBox = Microsoft.Maui.Controls.Compatibility.Platform.Tizen.Native.Box;
using NScroller = Microsoft.Maui.Controls.Compatibility.Platform.Tizen.Native.Scroller;
using Specific = Microsoft.Maui.Controls.PlatformConfiguration.TizenSpecific.ScrollView;

namespace Microsoft.Maui.Controls.Compatibility.Platform.Tizen
{
	[System.Obsolete(Compatibility.Hosting.MauiAppBuilderExtensions.UseMapperInstead)]
	public class ScrollViewRenderer : ViewRenderer<ScrollView, NScroller>
	{
		EContainer _scrollCanvas;
		int _defaultVerticalStepSize;
		int _defaultHorizontalStepSize;

		EvasBox EvasFormsCanvas => _scrollCanvas as EvasBox;

		NBox Canvas => _scrollCanvas as NBox;

		public ScrollViewRenderer()
		{
			RegisterPropertyHandler("Content", FillContent);
			RegisterPropertyHandler(ScrollView.OrientationProperty, UpdateOrientation);
			RegisterPropertyHandler(ScrollView.VerticalScrollBarVisibilityProperty, UpdateVerticalScrollBarVisibility);
			RegisterPropertyHandler(ScrollView.HorizontalScrollBarVisibilityProperty, UpdateHorizontalScrollBarVisibility);
			RegisterPropertyHandler(Specific.VerticalScrollStepProperty, UpdateVerticalScrollStep);
			RegisterPropertyHandler(Specific.HorizontalScrollStepProperty, UpdateHorizontalScrollStep);
		}

		public override ERect GetNativeContentGeometry()
		{
			return Forms.UseFastLayout ? EvasFormsCanvas.Geometry : Canvas.Geometry;
		}

		protected override void OnElementChanged(ElementChangedEventArgs<ScrollView> e)
		{
			if (Control == null)
			{
				SetNativeControl(CreateNativeControl());
				Control.Scrolled += OnScrolled;

				if (Forms.UseFastLayout)
				{
					_scrollCanvas = new EvasBox(Control);
					EvasFormsCanvas.LayoutUpdated += OnContentLayoutUpdated;
				}
				else
				{
					_scrollCanvas = new NBox(Control);
					Canvas.LayoutUpdated += OnContentLayoutUpdated;
				}

				Control.SetContent(_scrollCanvas);
				_defaultVerticalStepSize = Control.VerticalStepSize;
				_defaultHorizontalStepSize = Control.HorizontalStepSize;
			}

			if (e.OldElement != null)
			{
				(e.OldElement as IScrollViewController).ScrollToRequested -= OnScrollRequested;
			}

			if (e.NewElement != null)
			{
				(e.NewElement as IScrollViewController).ScrollToRequested += OnScrollRequested;
			}

			base.OnElementChanged(e);
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (ScrollView.ContentSizeProperty.PropertyName == e.PropertyName)
			{
				UpdateContentSize();
			}
			else
			{
				base.OnElementPropertyChanged(sender, e);
			}
		}

		protected virtual NScroller CreateNativeControl()
		{
			if (DeviceInfo.Idiom == DeviceIdiom.Watch)
			{
				return new Native.Watch.WatchScroller(Forms.NativeParent, Forms.CircleSurface);
			}
			else
			{
				return new NScroller(Forms.NativeParent);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (null != Element)
				{
					(Element as IScrollViewController).ScrollToRequested -= OnScrollRequested;
				}

				if (Control != null)
				{
					Control.Scrolled -= OnScrolled;
				}
				if (Canvas != null)
				{
					Canvas.LayoutUpdated -= OnContentLayoutUpdated;
				}
				if (EvasFormsCanvas != null)
				{
					EvasFormsCanvas.LayoutUpdated -= OnContentLayoutUpdated;
				}
			}

			base.Dispose(disposing);
		}

		void FillContent()
		{
			if (Forms.UseFastLayout)
			{
				EvasFormsCanvas.UnPackAll();
				if (Element.Content != null)
				{
					EvasFormsCanvas.PackEnd(Platform.GetOrCreateRenderer(Element.Content).NativeView);
					UpdateContentSize();
				}
			}
			else
			{
				Canvas.UnPackAll();
				if (Element.Content != null)
				{
					Canvas.PackEnd(Platform.GetOrCreateRenderer(Element.Content).NativeView);
					UpdateContentSize();
				}
			}
		}

		void OnContentLayoutUpdated(object sender, Native.LayoutEventArgs e)
		{
			// It is workaround,
			// in some case, before set a size of ScrollView, if content of content was filled with sized items,
			// after size of ScrollView was updated, a content position was moved to somewhere.
			if (Element.Content != null)
			{
				Platform.GetRenderer(Element.Content)?.NativeView?.Move(e.Geometry.X, e.Geometry.Y);
			}
			UpdateContentSize();
		}

		void UpdateOrientation()
		{
			switch (Element.Orientation)
			{
				case ScrollOrientation.Horizontal:
					Control.ScrollBlock = ScrollBlock.Vertical;
					Control.HorizontalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Auto;
					Control.VerticalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Invisible;
					break;
				case ScrollOrientation.Vertical:
					Control.ScrollBlock = ScrollBlock.Horizontal;
					Control.HorizontalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Invisible;
					Control.VerticalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Auto;
					break;
				default:
					Control.ScrollBlock = ScrollBlock.None;
					Control.HorizontalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Auto;
					Control.VerticalScrollBarVisiblePolicy = ScrollBarVisiblePolicy.Auto;
					break;
			}
		}

		void UpdateContentSize()
		{
			_scrollCanvas.MinimumWidth = Forms.ConvertToScaledPixel(Element.ContentSize.Width + Element.Padding.HorizontalThickness);
			_scrollCanvas.MinimumHeight = Forms.ConvertToScaledPixel(Element.ContentSize.Height + Element.Padding.VerticalThickness);

			// elm-scroller updates the CurrentRegion after render
			Application.Current.Dispatcher.Dispatch(() =>
			{
				if (Control != null)
				{
					OnScrolled(Control, EventArgs.Empty);
				}
			});
		}


		protected void OnScrolled(object sender, EventArgs e)
		{
			var region = Control.CurrentRegion.ToDP();
			((IScrollViewController)Element).SetScrolledPosition(region.X, region.Y);
		}

		async void OnScrollRequested(object sender, ScrollToRequestedEventArgs e)
		{
			var x = e.ScrollX;
			var y = e.ScrollY;
			if (e.Mode == ScrollToMode.Element)
			{
				Graphics.Point itemPosition = (Element as IScrollViewController).GetScrollPositionForElement(e.Element as VisualElement, e.Position);
				x = itemPosition.X;
				y = itemPosition.Y;
			}

			ERect region = new Graphics.Rect(x, y, Element.Width, Element.Height).ToEFLPixel();
			await Control.ScrollToAsync(region, e.ShouldAnimate);
			Element.SendScrollFinished();
		}

		void UpdateVerticalScrollBarVisibility()
		{
			Control.VerticalScrollBarVisiblePolicy = Element.VerticalScrollBarVisibility.ToPlatform();
		}

		void UpdateHorizontalScrollBarVisibility()
		{
			var orientation = Element.Orientation;
			if (orientation == ScrollOrientation.Horizontal || orientation == ScrollOrientation.Both)
				Control.HorizontalScrollBarVisiblePolicy = Element.HorizontalScrollBarVisibility.ToPlatform();
		}

		void UpdateVerticalScrollStep(bool initialize)
		{
			var step = Specific.GetVerticalScrollStep(Element);
			if (initialize && step == -1)
				return;

			Control.VerticalStepSize = step != -1 ? Forms.ConvertToScaledPixel(step) : _defaultVerticalStepSize;
		}

		void UpdateHorizontalScrollStep(bool initialize)
		{
			var step = Specific.GetHorizontalScrollStep(Element);
			if (initialize && step == -1)
				return;

			Control.HorizontalStepSize = step != -1 ? Forms.ConvertToScaledPixel(step) : _defaultHorizontalStepSize;
		}
	}

	static class ScrollBarExtensions
	{
		public static ScrollBarVisiblePolicy ToNative(this ScrollBarVisibility visibility)
		{
			switch (visibility)
			{
				case ScrollBarVisibility.Default:
					return ScrollBarVisiblePolicy.Auto;
				case ScrollBarVisibility.Always:
					return ScrollBarVisiblePolicy.Visible;
				case ScrollBarVisibility.Never:
					return ScrollBarVisiblePolicy.Invisible;
				default:
					return ScrollBarVisiblePolicy.Auto;
			}
		}
	}
}
