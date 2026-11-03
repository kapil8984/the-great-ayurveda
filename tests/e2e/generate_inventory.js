const fs = require('fs');
const path = require('path');

const rootDir = path.resolve(__dirname, '../..');
const outputDir = path.join(__dirname, 'output');
if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
}

function scanViews(dir, relativeRoot) {
    let list = [];
    if (!fs.existsSync(dir)) return list;
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const ent of entries) {
        const full = path.join(dir, ent.name);
        if (ent.isDirectory()) {
            list = list.concat(scanViews(full, relativeRoot));
        } else if (ent.name.endsWith('.cshtml') && !ent.name.startsWith('_')) {
            const relPath = path.relative(relativeRoot, full).replace(/\\/g, '/');
            list.push(relPath);
        }
    }
    return list;
}

const viewsBase = path.join(rootDir, 'src/Ayurveda.Web');
const allViews = [
    ...scanViews(path.join(viewsBase, 'Views'), viewsBase),
    ...scanViews(path.join(viewsBase, 'Areas'), viewsBase)
];

// Helper to determine route, auth, tier
function categorize(relPath) {
    // Examples:
    // Views/Home/Index.cshtml -> / -> None -> Tier 1
    // Views/Shop/Index.cshtml -> /Shop -> None -> Tier 1
    // Views/Shop/Details.cshtml -> /Shop/Details/1 -> None -> Tier 1
    // Views/Cart/Index.cshtml -> /Cart -> None -> Tier 1
    // Views/Checkout/Index.cshtml -> /Checkout -> Customer -> Tier 1
    // Views/Orders/Details.cshtml -> /Orders/Details/1 -> Customer -> Tier 1
    // Areas/Admin/Views/Dashboard/Index.cshtml -> /Admin/Dashboard -> Admin -> Tier 1
    // Areas/Admin/Views/Orders/Index.cshtml -> /Admin/Orders -> Admin -> Tier 1
    // ...

    let route = '/';
    let auth = 'None';
    let tier = 3;

    const parts = relPath.replace(/\.cshtml$/, '').split('/');

    if (parts[0] === 'Areas' && parts[1] === 'Admin') {
        auth = 'Admin';
        const ctrl = parts[3];
        const act = parts[4] || 'Index';
        const needsId = ['Details', 'Edit', 'Invoice'].includes(act);
        route = `/Admin/${ctrl}${act === 'Index' ? '' : '/' + act}${needsId ? '/1' : ''}`;
        if (ctrl === 'Dashboard' || ctrl === 'Orders' || ctrl === 'Inventory') {
            tier = 1;
        } else {
            tier = 2;
        }
    } else if (parts[0] === 'Views') {
        const ctrl = parts[1];
        const act = parts[2] || 'Index';

        if (ctrl === 'Home') {
            route = act === 'Index' ? '/' : `/${act}`;
            if (act === 'Index') tier = 1;
            else if (['About', 'Contact', 'Faq', 'DoshaQuiz', 'DoshaResult'].includes(act)) tier = 2;
            else tier = 3;
        } else if (ctrl === 'Shop') {
            if (act === 'Index') {
                route = '/Shop';
                tier = 1;
            } else if (act === 'Details') {
                route = '/Shop/Details/1';
                tier = 1;
            } else {
                route = `/Shop/${act}`;
                tier = 2;
            }
        } else if (ctrl === 'Cart') {
            route = '/Cart';
            tier = 1;
        } else if (ctrl === 'Checkout') {
            route = act === 'Confirmation' ? '/Checkout/Confirmation/1' : '/Checkout';
            auth = 'Customer';
            tier = 1;
        } else if (ctrl === 'Orders') {
            auth = 'Customer';
            if (act === 'Index') {
                route = '/Orders';
                tier = 1;
            } else if (act === 'Details') {
                route = '/Orders/Details/1';
                tier = 1;
            } else if (act === 'Invoice') {
                route = '/Orders/Invoice/1';
                tier = 2;
            } else {
                route = `/Orders/${act}`;
                tier = 2;
            }
        } else if (ctrl === 'Account') {
            if (['Login', 'Register', 'ForgotPassword', 'AccessDenied', 'VerifyOtp'].includes(act)) {
                route = `/Account/${act}`;
                auth = 'None';
                tier = 3;
            } else {
                route = `/Account/${act}`;
                auth = 'Customer';
                tier = 2;
            }
        } else if (ctrl === 'Doctors') {
            if (act === 'Index') {
                route = '/Doctors';
                tier = 2;
            } else if (act === 'Profile' || act === 'Details') {
                route = '/Doctors/Details/1';
                tier = 2;
            } else if (act === 'Consult' || act === 'Book') {
                route = '/Doctors/Book/1';
                auth = 'Customer';
                tier = 2;
            } else {
                route = `/Doctors/${act}`;
                tier = 2;
            }
        } else if (ctrl === 'Articles' || ctrl === 'Blog') {
            route = act === 'Index' ? `/${ctrl}` : `/${ctrl}/Details/1`;
            tier = 2;
        } else if (ctrl === 'Dosha') {
            route = act === 'Index' ? '/Dosha' : `/Dosha/${act}`;
            tier = 2;
        } else {
            route = act === 'Index' ? `/${ctrl}` : `/${ctrl}/${act}`;
            tier = 3;
        }
    }

    return { relPath, route, auth, tier };
}

const inventory = allViews.map(categorize);

// Sort: Tier asc, Route asc
inventory.sort((a, b) => a.tier - b.tier || a.route.localeCompare(b.route));

const csvHeader = 'RelativePath,RouteUrl,RequiresAuth,Tier\n';
const csvRows = inventory.map(item => `"${item.relPath}","${item.route}","${item.auth}",${item.tier}`).join('\n');

const csvPath = path.join(outputDir, 'ui_inventory.csv');
fs.writeFileSync(csvPath, csvHeader + csvRows + '\n');
console.log(`Saved inventory of ${inventory.length} views to ${csvPath}`);
console.log(JSON.stringify(inventory.slice(0, 10), null, 2));
