<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AddJob.aspx.cs" Inherits="APSIM.Cloud.Portal.Upload" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <style type="text/css">
       #Text1
       {
          width: 195px;
          height: 24px;
       }
    </style>
</head>
<body>
    <form id="form1" runat="server">
    <p>
        Select .xml or .zip file to add to job queue:&nbsp;&nbsp;
       <asp:FileUpload ID="FileUpload" runat="server" Width="313px" Height="20px" />
    </p>
    <asp:Panel ID="Panel1" runat="server">
        <asp:Label ID="Label1" runat="server" Text="Enter 'now' date (dd/mm/yyyy):"></asp:Label>
        <asp:TextBox ID="NowEditBox" runat="server"></asp:TextBox>
    </asp:Panel>
    <p>
       <asp:Button ID="UploadButton" runat="server" onclick="UploadButtonClick" 
          Text="Upload" />
    </p>
    </form>
</body>
</html>
