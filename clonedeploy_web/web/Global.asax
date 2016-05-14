﻿<%@ Application Language="C#" %>
<%@ Import Namespace="System.Web.Http" %>
<%@ Import Namespace="System.Web.Routing" %>
<%@ Import Namespace="Swashbuckle.Application" %>


<script runat="server">
 
    void Application_Start(object sender, EventArgs e)
    {
        GlobalConfiguration.Configuration
            .EnableSwagger(c =>
            {
                c.SingleApiVersion("v1", "CloneDeploy Web API");
               
            })
             .EnableSwaggerUi(c =>
        {
           
        });
            

     

        GlobalConfiguration.Configuration.Formatters.XmlFormatter.SupportedMediaTypes.Clear();

        RouteTable.Routes.MapHttpRoute(
            name: "DefaultApi",
            routeTemplate: "api/{controller}/{action}/{id}",
            defaults: new {id = System.Web.Http.RouteParameter.Optional}
            );
        
    }

    void Application_End(object sender, EventArgs e) 
    {
        //  Code that runs on application shutdown

    }
        
    void Application_Error(object sender, EventArgs e) 
    { 
        // Code that runs when an unhandled error occurs

    }

    void Session_Start(object sender, EventArgs e) 
    {
        // Code that runs when a new session is started

    }

    void Session_End(object sender, EventArgs e) 
    {
        // Code that runs when a session ends. 
        // Note: The Session_End event is raised only when the sessionstate mode
        // is set to InProc in the Web.config file. If session mode is set to StateServer 
        // or SQLServer, the event is not raised.

    }
       
</script>
