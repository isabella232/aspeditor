// AspNetEdit.Editor.Persistence.ControlPersister
//	based on Mono's System.Web.UI.Design.ControlPersister
//
// Authors:
//      Gert Driesen (drieseng@users.sourceforge.net)
//	Michael Hutchinson <m.j.hutchinson@gmail.com>
//
// (C) 2004 Novell
// (c) 2205 Michael Hutchinson

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Web.UI;
using System.Web.UI.Design;

namespace AspNetEdit.Editor.Persistence
{
	public sealed class ControlPersister
	{
		private ControlPersister ()
		{
		}

		#region Public members. They call private methods with some checking and restrictions.

		public static string PersistControl (Control control)
		{
			if (control.Site == null)
				return string.Empty;

			IDesignerHost host = control.Site.GetService (typeof(IDesignerHost)) as IDesignerHost;

			return PersistControl (control, host);
		}

		public static void PersistControl (TextWriter sw, Control control)
		{
			if (control.Site == null)
				return;

			IDesignerHost host = control.Site.GetService (typeof(IDesignerHost)) as IDesignerHost;

			PersistControl (sw, control, host);
		}

		public static string PersistControl (Control control, IDesignerHost host)
		{
			TextWriter writer = new StringWriter ();

			PersistControl (writer, control, host);

			writer.Flush ();
			return writer.ToString ();
		}

		public static void PersistControl(TextWriter sw, Control control, IDesignerHost host)
		{
			//check input
			if (host == null)
				throw new ArgumentNullException ("host");
			if (control == null)
				throw new ArgumentNullException ("control");
			if (sw == null)
				throw new ArgumentNullException ("sw");
			
			//We use an HtmlTextWriter for output
			HtmlTextWriter writer;
			if (sw is HtmlTextWriter)
				writer = (HtmlTextWriter) sw;
			else
				writer = new HtmlTextWriter (sw);

			PersistObject(writer, control, host, true);
		}

		public static string PersistInnerProperties (object component, IDesignerHost host)
		{
			TextWriter sw = new StringWriter ();

			PersistInnerProperties (sw, component, host);

			sw.Flush();
			return sw.ToString();
		}

		public static void PersistInnerProperties (TextWriter sw, object component, IDesignerHost host)
		{
			//check input
			if (host == null)
				throw new ArgumentNullException ("host");
			if (component == null)
				throw new ArgumentNullException ("component");

			if (!(component is System.Web.UI.Control))
				throw new InvalidOperationException ("Only components that derive from System.Web.UI.Control can be serialised");

			//privte method needs an HtmlTextWriter
			HtmlTextWriter writer;
			if (sw is HtmlTextWriter)
				writer = (HtmlTextWriter) sw;
			else
				writer = new HtmlTextWriter (sw);

			//write and flush
			PersistInnerProperties (writer, component, host);
			writer.Flush();
		}

		#endregion

		private static void PersistObject (HtmlTextWriter writer, object control, IDesignerHost host, bool runAtServer)
		{
			//look up tag prefix from host
			IWebFormReferenceManager refMan = host.GetService (typeof (IWebFormReferenceManager)) as IWebFormReferenceManager;
			if (refMan == null)
				throw new Exception("Could not obtain IWebFormReferenceManager service");
			string prefix = refMan.GetTagPrefix (control.GetType ());
			
			//write tag to HtmlTextWriter
			writer.WriteBeginTag (prefix + ":" + control.GetType().Name);
			
			//go through all the properties and add attributes if necessary
			PropertyDescriptorCollection properties = TypeDescriptor.GetProperties (control);
			foreach (PropertyDescriptor prop in properties)
				ProcessAttribute (prop, control, writer, string.Empty);
			
			if (runAtServer)
				writer.WriteAttribute ("runat", "server");
			
			//do the same for events
			IComponent comp = control as IComponent;
			if (comp != null && comp.Site != null) {
				IEventBindingService evtBind = (IEventBindingService) comp.Site.GetService (typeof (IEventBindingService));
				if (evtBind != null)
					foreach (EventDescriptor e in TypeDescriptor.GetEvents (comp))
						ProcessEvent (e, comp, writer, evtBind);
			}	
			

			//ControlDesigner designer = (ControlDesigner) host.GetDesigner(control);
			//TODO: we don't yet support designer.GetPersistInnerHtml() 'cause we don't have the designers...
			if (HasInnerProperties(control)) {
				writer.Write (HtmlTextWriter.TagRightChar);
				writer.Indent++;
				PersistInnerProperties (writer, control, host);
				writer.Indent--;
				writer.WriteEndTag (prefix + ":" + control.GetType ().Name);
			}
			else
				writer.Write (HtmlTextWriter.SelfClosingTagEnd);
			
			writer.WriteLine ();
			writer.Flush ();
		}
		
		private static void ProcessEvent (EventDescriptor e, IComponent comp, HtmlTextWriter writer, IEventBindingService evtBind)
		{
			PropertyDescriptor prop = evtBind.GetEventProperty (e);
			string value = prop.GetValue (comp) as string;
			
			if (prop.SerializationVisibility != DesignerSerializationVisibility.Visible
				|| value == null
				|| prop.DesignTimeOnly
				|| prop.IsReadOnly
				|| !prop.ShouldSerializeValue (comp))
				return;
			
			writer.WriteAttribute ("On" + prop.Name, value);
		}

		/// <summary>
		/// Writes an attribute to an HtmlTextWriter if it needs serializing
		/// </summary>
		/// <returns>True if it does any writing</returns>
		private static bool ProcessAttribute (PropertyDescriptor prop, object o, HtmlTextWriter writer, string prefix)
		{
			//check whether we're serialising it
			if (prop.SerializationVisibility == DesignerSerializationVisibility.Hidden
				|| prop.DesignTimeOnly
				|| prop.IsReadOnly
				|| !prop.ShouldSerializeValue (o)
				|| prop.Converter == null
				|| !prop.Converter.CanConvertTo (typeof(string)))
				return false;	

			bool foundAttrib = false;
				
			//is this an attribute? If it's content, we deal with it later.		
			PersistenceModeAttribute modeAttrib = prop.Attributes[typeof (PersistenceModeAttribute)] as PersistenceModeAttribute;
			if (modeAttrib == null || modeAttrib.Mode == PersistenceMode.Attribute)
			{
				if (prop.SerializationVisibility == DesignerSerializationVisibility.Visible) {
					if (prefix == string.Empty)
						writer.WriteAttribute (prop.Name, prop.Converter.ConvertToString (prop.GetValue (o)));
					else
						writer.WriteAttribute (prefix + "-" + prop.Name, prop.Converter.ConvertToString (prop.GetValue(o)));
					foundAttrib = true;
				}
				//recursively handle subproperties
				else if (prop.SerializationVisibility == DesignerSerializationVisibility.Content) {
					object val = prop.GetValue (o);
					foreach (PropertyDescriptor p in prop.GetChildProperties (val))
						if (ProcessAttribute (p, val, writer, prop.Name))
							foundAttrib = true;
				}
			}
			return foundAttrib;
		}
		
		private static void PersistInnerProperties (HtmlTextWriter writer, object component, IDesignerHost host)
		{
			//Do we have child controls as inner content of control?
			PersistChildrenAttribute persAtt = TypeDescriptor.GetAttributes (component)[typeof (PersistChildrenAttribute)] as PersistChildrenAttribute;
			if (persAtt != null && persAtt.Persist && (component is Control))
			{
				if (((Control)component).Controls.Count > 0)
				{
					writer.Indent++;
					foreach (Control child in ((Control) component).Controls) {
						PersistControl (writer, child, host);
					}
					writer.Indent--;
				}
			}
			//We don't, so we're going to have to go though the properties
			else
			{
				PropertyDescriptorCollection properties = TypeDescriptor.GetProperties (component);
				bool contentStarted = false;
				foreach (PropertyDescriptor prop in properties)
				{
					//check whether we're serialising it
					if (prop.SerializationVisibility == DesignerSerializationVisibility.Hidden
						|| prop.DesignTimeOnly
						//|| !prop.ShouldSerializeValue (component) //confused by collections...
						|| prop.Converter == null)
						continue;

					PersistenceModeAttribute modeAttrib = prop.Attributes[typeof(PersistenceModeAttribute)] as PersistenceModeAttribute;
					if (modeAttrib == null || modeAttrib.Mode == PersistenceMode.Attribute)
						continue;

					//handle the different modes
					switch (modeAttrib.Mode)
					{
						case PersistenceMode.EncodedInnerDefaultProperty:
							if (contentStarted)
								throw new Exception("The Control has inner properties in addition to a default inner property");
							if (prop.Converter.CanConvertTo (typeof (string))){
								writer.Write(System.Web.HttpUtility.HtmlEncode (prop.Converter.ConvertToString (prop.GetValue (component))));
								return;
							}
							break;
						case PersistenceMode.InnerDefaultProperty:
							if (contentStarted)
								throw new Exception("The Control has inner properties in addition to a default inner property");
							PersistInnerProperty(prop,prop.GetValue (component), writer, host, true);
							return;
						case PersistenceMode.InnerProperty:
							PersistInnerProperty (prop, prop.GetValue (component), writer, host, false);
							contentStarted = true;
							break;
					}
				}
				writer.WriteLine();
			}
		}

		//once we've determined we need to persist a property, this does the actual work
		private static void PersistInnerProperty (PropertyDescriptor prop, object value, HtmlTextWriter writer, IDesignerHost host, bool isDefault)
		{
			//look up tag prefix from host
			IWebFormReferenceManager refMan = host.GetService (typeof (IWebFormReferenceManager)) as IWebFormReferenceManager;
			if (refMan == null)
				throw new Exception("Could not obtain IWebFormReferenceManager service"); ;
			string prefix = refMan.GetTagPrefix (prop.PropertyType);


			//newline and indent
			writer.WriteLine();

			//trivial case
			if (value == null) {
				if (!isDefault) {
					writer.WriteBeginTag (prefix + ":" + prop.Name);
					writer.Write (HtmlTextWriter.SelfClosingTagEnd);
				}
				return;
			}


			//Persist collections as tag with property name
			//containing tags of object name, with object properties as attribs
			if (value is ICollection) {
				if (((ICollection) value).Count > 0) {
					if(!isDefault) {
						writer.WriteFullBeginTag(prefix + ":" + prop.Name);
						writer.Indent++;
					}
					
					foreach (object o in (ICollection)value)
						PersistObject (writer, o, host, false);

					if(!isDefault) {
						writer.Indent--;
						writer.WriteEndTag (prefix + ":" + prop.Name);
					}
				}
			}
			//not a collection? Simple: a tag of object name, with object properties as attribs
			else
			{		
				//only want to render tag if it has any attributes
				writer.WriteBeginTag (prefix + ":" + prop.Name);

				foreach (PropertyDescriptor p in TypeDescriptor.GetProperties(value))
					ProcessAttribute (p, value, writer, string.Empty);

				writer.Write (HtmlTextWriter.SelfClosingTagEnd);
			}
		}

		//simply checks if there are any inner properties to render so we can use self-closing tags
		private static bool HasInnerProperties (object component)
		{
			if (component == null)
				throw new ArgumentNullException ("component");


			//Do we have child controls as inner content of control?
			PersistChildrenAttribute persAtt = TypeDescriptor.GetAttributes (component)[typeof(PersistChildrenAttribute)] as PersistChildrenAttribute;
			if (persAtt != null && persAtt.Persist && (component is Control))
			{
				return true;
			}
			//We don't, so we're going to have to go though the properties
			else
			{
				PropertyDescriptorCollection properties = TypeDescriptor.GetProperties (component);
				foreach (PropertyDescriptor prop in properties)
				{
					//check whether we're serialising it
					if (prop.SerializationVisibility == DesignerSerializationVisibility.Hidden
						|| prop.DesignTimeOnly
						//|| !prop.ShouldSerializeValue(component) //confused by collections....
						|| prop.Converter == null)
						continue;

					PersistenceModeAttribute modeAttrib = prop.Attributes[typeof (PersistenceModeAttribute)] as PersistenceModeAttribute;
					if (modeAttrib == null || modeAttrib.Mode == PersistenceMode.Attribute)
						continue;

					return true;
				}
			}

			return false;
		}
	}
}