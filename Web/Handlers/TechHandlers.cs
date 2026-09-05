using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>科技树接口</summary>
internal static class TechHandlers
{
    internal static void Register()
    {
        Router.Add("GET", "/api/techtree/diagnose", _ =>
            MainThread.Run(() => TechTreeService.DiagnoseTechTree(), 10000));

        Router.Add("GET", "/api/techtree", _ =>
            MainThread.Run(() => TechTreeService.GetTechTreeJson(), 10000));

        Router.Add("POST", "/api/techtree/toggle", ctx =>
        {
            int techId = ctx.JsonInt("techId");
            bool unlock = ctx.JsonBool("unlock", true);
            if (techId == 0) throw new HttpError(400, "missing techId");
            return MainThread.Run(() => TechTreeService.ToggleTechUnlock(techId, unlock));
        });

        Router.Add("POST", "/api/techtree/unlockall", _ =>
            MainThread.Run(() => TechTreeService.UnlockAllTechs(), 10000));

        Router.Add("POST", "/api/techtree/research", ctx =>
        {
            int techId = ctx.JsonInt("techId");
            if (techId == 0) throw new HttpError(400, "missing techId");
            return MainThread.Run(() => TechTreeService.SetResearchTech(techId));
        });
    }
}
