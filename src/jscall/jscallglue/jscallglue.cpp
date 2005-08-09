/* 
 * jscallglue.cpp - XPCOM DOM interop for communication between JavaScript and Mono/Gecko#
 * 
 * Authors: 
 *  Michael Hutchinson <m.j.hutchinson@gmail.com>
 *  
 * Copyright (C) 2005 Michael Hutchinson
 *
 * This sourcecode is licenced under The MIT License:
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

#include <gtkmozembed.h>
#include <gtkmozembed_internal.h>

#include <nsCOMPtr.h>
#include <nsString.h>

#include <nsIWebBrowser.h>
#include <nsIDOMWindow.h>
#include <nsIDOMDocument.h>
#include <nsIDOMElement.h>
#include <nsIDOMNode.h>
#include <nsIDOMNodeList.h>

extern "C"
{
  int PlaceFunctionCall(GtkMozEmbed *embed, char *call, char*returnto, char *args);
  //void CollectFunctionCall(GtkMozEmbed *embed, char *call, char *args, char*returnto);
}



/*
 Gets the browser's IDomDocument.
*/
nsCOMPtr<nsIDOMDocument> GetIDOMDocument(GtkMozEmbed *embed)
{
    //Get browser
    nsCOMPtr<nsIWebBrowser> browser;
    gtk_moz_embed_get_nsIWebBrowser(embed, getter_AddRefs(browser));
    //if(!browser) return ;
    
    nsCOMPtr<nsIDOMWindow> domWindow;
    browser->GetContentDOMWindow(getter_AddRefs(domWindow));

    nsCOMPtr<nsIDOMDocument> domDocument;
    domWindow->GetDocument(getter_AddRefs(domDocument));
    return domDocument;
}


/*
 Places a <outfunction call="..." args="..." returnto="..." />
 function call element in the <jscall> element.
*/
int PlaceFunctionCall(GtkMozEmbed *embed, char *call, char*returnto, char *args)
{
	nsresult result;
	
    nsCOMPtr<nsIDOMDocument> doc = GetIDOMDocument(embed);
        
    //get the "jscall" node
    nsCOMPtr<nsIDOMNodeList> nodes;
    result = doc->GetElementsByTagName(NS_ConvertUTF8toUTF16("jscall"), getter_AddRefs(nodes));
    if(NS_FAILED(result) || !nodes) return 1;
    
    PRUint32 length = 0;
    result = nodes -> GetLength(&length);
    if(NS_FAILED(result)) return 2;
    if(length != 1) return 3;
    
    nsCOMPtr<nsIDOMNode> jscall;
    result = nodes->Item(0, getter_AddRefs(jscall));
    if(NS_FAILED(result) || !jscall) return 4;

    //add an "infunction" node
    nsCOMPtr<nsIDOMElement> infunction;
    result = doc->CreateElement(NS_ConvertUTF8toUTF16("infunction"), getter_AddRefs(infunction));
    if(NS_FAILED(result) || !infunction) return 5;
    
    //add the properties
    result = infunction->SetAttribute(NS_ConvertUTF8toUTF16("call"), NS_ConvertUTF8toUTF16(call));
    result = infunction->SetAttribute(NS_ConvertUTF8toUTF16("returnto"), NS_ConvertUTF8toUTF16(returnto));
    result = infunction->SetAttribute(NS_ConvertUTF8toUTF16("args"), NS_ConvertUTF8toUTF16(args));
    if(NS_FAILED(result)) return 6;
    
    //append it to the jscall node
    nsCOMPtr<nsIDOMNode> domNodeInfunction = do_QueryInterface(infunction);
    if(!domNodeInfunction) return 7;//return 9;
    nsCOMPtr<nsIDOMNode> outchild;
    result = jscall->AppendChild(domNodeInfunction, getter_AddRefs(outchild));
    if(NS_FAILED(result) || !jscall) return 8;
    
    return 0;
}



/*
 Retrieves an <infunction call="..." args="..." returnto="..." />
 function call element from the <jscall> element.
 
 NB: Untested, and now deprecated to simplify interop.
     May reinstate to improve performance.
*/
/*
void CollectFunctionCall(GtkMozEmbed *embed, char *call, char *args, char*returnto)
{
    nsCOMPtr<nsIDOMDocument> doc = GetIDOMDocument(embed);
    
    //get the "outfunction" node
    nsCOMPtr<nsIDOMNodeList> nodes;
    doc->GetElementsByTagName(NS_LITERAL_STRING("outfunction"), getter_AddRefs(nodes));
    PRUint32 length;
    nodes->GetLength(&length);
    if(length != 1) return;// false;
    nsCOMPtr<nsIDOMNode> domNodeOutfunction;
    nodes->Item(0, getter_AddRefs(domNodeOutfunction));

    //check it's what we want
    nsCOMPtr<nsIDOMNode> parent;
    domNodeOutfunction->GetParentNode(getter_AddRefs(parent));
    nsAutoString str;
    str = parent->GetNodeName(str);
    if(!(str.Equals(NS_LITERAL_STRING("jscall")))) return;// false;

    PRUint16 type;
    domNodeOutfunction->GetNodeType(&type);
    if(type != nsIDOMNode::ELEMENT_NODE) return;// false; 

    //get the data from the attributes
    nsCOMPtr<nsIDOMElement> outfunction = do_QueryInterface(domNodeOutfunction);

    PRBool valid;
    outfunction->HasAttribute(NS_LITERAL_STRING("call"), &valid);
    if(!valid) return; //false;

    outfunction->GetAttribute(NS_LITERAL_STRING("call"), str);
    call = ToNewUTF8String(str);
    outfunction->GetAttribute(NS_LITERAL_STRING("args"), str);
    args = ToNewUTF8String(str);
    outfunction->GetAttribute(NS_LITERAL_STRING("returnto"), str);
    returnto = ToNewUTF8String(str);

    //remove node
    nsCOMPtr<nsIDOMNode> outChild;
    parent->RemoveChild(domNodeOutfunction, getter_AddRefs(outChild));
    
    return;// true;
}
*/
