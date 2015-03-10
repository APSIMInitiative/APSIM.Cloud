<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Main.aspx.cs" Inherits="APSIM.Cloud.WebPortal.WebForm1" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    
       <h1>
           APSIM.Cloud job system</h1>
    
    </div>
    <p style="margin-left: 0px; margin-bottom: 19px">
    &nbsp;&nbsp;&nbsp;&nbsp; Number of rows:
       <asp:TextBox ID="NumRowsTextBox" runat="server" AutoPostBack="True" 
          ontextchanged="NumRowsTextBox_TextChanged">100</asp:TextBox>
&nbsp;&nbsp;
       </p>
    <p>
       <asp:GridView ID="GridView" runat="server" 
          CellPadding="4" ForeColor="#333333" 
          GridLines="Vertical" AutoGenerateColumns="False" 
            onrowcommand="GridView_RowCommand">
          <AlternatingRowStyle BackColor="White" ForeColor="#284775" />
           <Columns>
               <asp:ButtonField ButtonType="Image" CommandName="ReRun" 
                   ImageUrl="~/Resources/Rerun.png" Text="Rerun" />
               <asp:ButtonField ButtonType="Image" CommandName="Delete" 
                   ImageUrl="~/Resources/delete2.png" Text="Delete" />
               <asp:BoundField DataField="Name" HeaderText="Name" />
               <asp:BoundField DataField="Status" HeaderText="Status" />
               <asp:ButtonField DataTextField="XML" Text="Button" CommandName="XML" >
               </asp:ButtonField>
               <asp:HyperLinkField HeaderText="Download zip" Text="Download zip" 
                   DataNavigateUrlFields="URL" />
           </Columns>
          <EditRowStyle BackColor="#999999" />
          <FooterStyle BackColor="#5D7B9D" Font-Bold="True" ForeColor="White" />
          <HeaderStyle BackColor="#5D7B9D" Font-Bold="True" ForeColor="White" />
          <PagerStyle BackColor="#284775" ForeColor="White" HorizontalAlign="Center" />
          <RowStyle BackColor="#F7F6F3" ForeColor="#333333" VerticalAlign="Top" />
          <SelectedRowStyle BackColor="#E2DED6" Font-Bold="True" ForeColor="#333333" />
          <SortedAscendingCellStyle BackColor="#E9E7E2" />
          <SortedAscendingHeaderStyle BackColor="#506C8C" />
          <SortedDescendingCellStyle BackColor="#FFFDF8" />
          <SortedDescendingHeaderStyle BackColor="#6F8DAE" />
       </asp:GridView>
    </p>
    <p>
       &nbsp;</p>
    <p>
       &nbsp;</p>
    <p>
       &nbsp;</p>
    </form>
</body>
</html>
