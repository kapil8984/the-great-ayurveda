const fs = require('fs');
const path = require('path');

const baseDir = path.resolve(__dirname, '..');
const layoutPath = path.join(baseDir, 'src/Ayurveda.Web/Areas/Admin/Views/Shared/_Layout.cshtml');
const controllersDir = path.join(baseDir, 'src/Ayurveda.Web/Areas/Admin/Controllers');
const viewsDir = path.join(baseDir, 'src/Ayurveda.Web/Areas/Admin/Views');

const layoutContent = fs.readFileSync(layoutPath, 'utf8');

// Regex for <a asp-controller="..." asp-action="..." class="admin-sidebar-link">
const linkRegex = /<a\s+asp-controller="([^"]+)"\s+asp-action="([^"]+)"[^>]*>([\s\S]*?)<\/a>/g;

let match;
const links = [];

while ((match = linkRegex.exec(layoutContent)) !== null) {
    const controller = match[1];
    const action = match[2];
    const rawInner = match[3].replace(/<[^>]+>/g, '').trim();
    links.push({ controller, action, label: rawInner });
}

console.log('| Label | Sidebar Target | Controller File | Action Method | View Path | View Exists | Has Empty State |');
console.log('| :--- | :--- | :--- | :--- | :--- | :---: | :---: |');

links.forEach(l => {
    let ctrlFile = path.join(controllersDir, `${l.controller}Controller.cs`);
    let ctrlExists = fs.existsSync(ctrlFile);
    if (!ctrlExists) {
        // Check OtherAdminControllers.cs
        const otherCtrl = path.join(controllersDir, 'OtherAdminControllers.cs');
        if (fs.existsSync(otherCtrl)) {
            const content = fs.readFileSync(otherCtrl, 'utf8');
            if (content.includes(`class ${l.controller}Controller`)) {
                ctrlFile = otherCtrl;
                ctrlExists = true;
            }
        }
    }

    let actionExists = false;
    if (ctrlExists) {
        const ctrlCode = fs.readFileSync(ctrlFile, 'utf8');
        actionExists = new RegExp(`public\\s+(?:async\\s+Task<IActionResult>|IActionResult)\\s+${l.action}\\s*\\(`, 'i').test(ctrlCode);
    }

    // Expected view path
    let viewFile = path.join(viewsDir, l.controller, `${l.action}.cshtml`);
    let viewExists = fs.existsSync(viewFile);

    let hasEmptyState = 'N/A';
    if (viewExists) {
        const viewCode = fs.readFileSync(viewFile, 'utf8');
        const emptyTerms = ['!Model.Any()', '.Count == 0', 'No ', 'empty', 'alert-info', 'text-muted text-center', 'Nothing found'];
        const matches = emptyTerms.some(t => viewCode.toLowerCase().includes(t.toLowerCase()));
        hasEmptyState = matches ? 'Yes' : 'Review';
    }

    const relView = path.relative(baseDir, viewFile).replace(/\\/g, '/');
    const relCtrl = path.relative(baseDir, ctrlFile).replace(/\\/g, '/');

    console.log(`| ${l.label} | /admin-portal/${l.controller.toLowerCase()}/${l.action === 'Index' ? '' : l.action.toLowerCase()} | \`${path.basename(relCtrl)}\` | ${actionExists ? 'Yes' : 'MISSING'} | \`${path.basename(relView)}\` | ${viewExists ? 'Yes' : 'MISSING'} | ${hasEmptyState} |`);
});
