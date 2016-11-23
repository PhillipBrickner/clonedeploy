﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using CloneDeploy_App.Controllers.Authorization;
using CloneDeploy_App.DTOs;
using CloneDeploy_App.Models;

namespace CloneDeploy_App.Controllers
{
    public class ComputerProxyReservationController : ApiController
    {

        [ComputerAuth(Permission = "ComputerRead")]
        public IHttpActionResult Get(int id)
        {
            var reservation = BLL.ComputerProxyReservation.GetComputerProxyReservation(id);
            if (reservation == null)
                return NotFound();
            else
                return Ok(reservation);
        }

        [HttpGet]
        [ComputerAuth(Permission = "ComputerUpdate")]
        public ApiBoolDTO Toggle(int id, bool status)
        {
            var result = new ApiBoolDTO();
            result.Value = BLL.ComputerProxyReservation.ToggleProxyReservation(id, status);
            return result;
        }

        [ComputerAuth(Permission = "ComputerUpdate")]
        public Models.ActionResult Put(Models.ComputerProxyReservation computerProxyReservation)
        {
            var actionResult = new ActionResult();
            actionResult.Success = BLL.ComputerProxyReservation.UpdateComputerProxyReservation(computerProxyReservation);
            if (!actionResult.Success)
            {
                var response = Request.CreateResponse(HttpStatusCode.NotFound, actionResult);
                throw new HttpResponseException(response);
            }
            return actionResult;
        }
    }
}