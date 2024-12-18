using JJ_AzFnBlobToSharePoint.Core.Exceptions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class ExceptionManager
    {
        public static ActivityErrorLog SetActivityErrorEntity(Exception ExpMsg, string ActivityStage) {

            var activityErrorLog = new ActivityErrorLog { ActivityStage = ActivityStage };
            if (ExpMsg is ProcessException proExp)
            {
                activityErrorLog.ErrorMessage = proExp.Message;
                activityErrorLog.Container = proExp.Container;
                activityErrorLog.FileName = proExp.FileName;
            }
            else
                activityErrorLog.ErrorMessage = ExpMsg.Message;

            return activityErrorLog;
        }

    }
}
