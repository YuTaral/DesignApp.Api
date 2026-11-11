// Builds "DesignApp - Architecture Overview.docx": the system design explained at a high level.
const { d, styles, numbering, p, h1, h2, bullet, step, title, table, footer } = require('./common')
const { Document, Packer } = d

// Portrait A4, 2 cm margins: usable width 11906 - 2*1134 = 9638.
const children = [
  ...title('DesignApp: Architecture Overview', 'How the system is put together, and why'),

  h1('1. What the system does'),
  p('DesignApp lets someone photograph a room and try new colors on anything in it. The user uploads a photo; the AI finds the walls, floor, furniture and decor and outlines each one; then the user taps any object and recolors it, with its shading and texture kept, so it looks painted rather than tinted. Where the AI\'s outline is wrong, the user fixes it with a brush. Anyone can try it without an account; signing in saves rooms and their colors into designs, each holding one or more rooms, that open again on any device. Uploads that are never saved are deleted after a day.'),
  p('Two things shape the whole design:'),
  bullet('**A live editor.** Recoloring, hover outlines, before/after compare and the brush preview change dozens of times per second, so they must run in the browser.'),
  bullet('**Slow, paid, swappable AI.** An analysis takes 13–20 seconds, costs about 3 cents, and has been swapped several times (fake AI, real AI, recorded AI, Haiku to Sonnet).'),

  h1('2. The big picture'),
  p('The system has three parts that run side by side, started together by .NET Aspire with one F5 in Visual Studio, and it calls two external AI services:'),
  table(['Part', 'Role', 'Technology'], [
    ['Web app', 'Everything the user sees: upload, live editor, brush, undo, sign-in, My designs and the rooms of each design. Runs entirely in the browser.', 'React 19 single-page app, WebGL2, TanStack Query, Zustand'],
    ['API', 'Accounts and guests, stores designs and their rooms, runs the AI pipeline in the background, serves JSON and images, pushes live progress.', '.NET 10 Minimal APIs, ASP.NET Core Identity, EF Core, SignalR, background worker'],
    ['Storage', 'Accounts, rooms, their objects and saved designs in a database; photos and outline maps as files.', 'PostgreSQL, RustFS (S3-compatible)'],
    ['AI services (external)', 'Claude lists the objects; SAM 3 outlines them all in one request.', 'Anthropic API (Claude Sonnet 5.5), fal.ai (SAM 3)'],
  ], [1900, 4600, 3138]),

  h2('How a photo flows through the system'),
  step('**Upload.** The browser sends the photo with two choices: what the AI should find, and (in development) fake or real AI. The API cleans the photo (upright, sRGB, metadata removed), stores it, saves the room under the account (or this browser\'s guest id) and answers at once.'),
  step('**Analysis in the background.** A worker picks up the room. Claude lists the objects with boxes, and a few points on each wall, floor and ceiling; one SAM 3 request traces every object\'s exact outline from those boxes and points; the outlines are combined into a label map, an image in which every pixel holds the number of the object it belongs to, and cleaned up.'),
  step('**Live progress.** Each step is saved and pushed to the browser over SignalR, so the user watches "Finding objects" and "Outlining objects" happen.'),
  step('**Editing in the browser.** The browser loads the photo and the label map once. From then on, selecting and recoloring are pure browser work: a pixel lookup finds the object under the finger, and a WebGL shader recolors it every frame. No server round trips.'),
  step('**Outline fixes.** A brush stroke is drawn live in the browser, then sent to the API, which applies the exact same rules and stores the result as a new outline version. Undo switches back to an earlier version.'),
  step('**Saving.** A signed-in user saves the room into a new design (a name and a short description) or into an existing one; a design\'s page can also add rooms by uploading straight into it. Each room keeps its own colors: just a list of colors per object, never a changed photo. Removing a room deletes it; an upload not saved to any design is deleted after 24 hours.'),

  h1('3. Backend: Clean Architecture'),
  p('The API is split into four layers. Each layer may only depend on the layers inside it, so the core rules never know about databases, HTTP or AI vendors.'),
  table(['Layer', 'Contains', 'Depends on'], [
    ['Domain (center)', 'The business rules: a Room and its lifecycle, detected objects, colors, boxes. Pure C#.', 'Nothing'],
    ['Application', 'One class per use case, each in its own file in the UseCases folder (Designs or Rooms, by the entity it changes), next to Interfaces (the ports) and Objects (the records) (upload, analyze, fix outline, get a room, save a guest\'s rooms to the account, create and edit designs, add, color and remove their rooms, delete unsaved uploads) and the interfaces they need: "something that lists objects", "something that stores files", "who is the current user".', 'Domain'],
    ['Infrastructure', 'The real implementations of those interfaces: Claude, SAM 3, the fake AI, Postgres (with Identity\'s account tables), S3 storage, image processing, the background worker.', 'Application, Domain'],
    ['Api (outside)', 'HTTP endpoints, JSON shapes, sign-in and guest cookies, SignalR hub, error handling, rate limits, wiring everything together.', 'All of the above'],
  ], [2200, 5600, 1838]),
  p('This is also called "ports and adapters": the Application layer defines ports (interfaces), and Infrastructure plugs adapters into them. Swapping Claude for another model, or S3 for Azure Blob Storage, means writing one new adapter; no use case changes.'),

  h2('The ideas that matter most'),
  bullet('**The domain guards itself.** A room can only move forward through its statuses, can\'t be Ready without objects, and only switches to outline versions that exist. Bugs elsewhere can\'t create an impossible room.'),
  bullet('**Expected problems are values, not exceptions.** A blurry upload or a brush stroke that changes nothing comes back as a refusal with a message for the user. Exceptions are reserved for real failures, which users never see the details of. A request the browser abandoned (a page left mid-load) simply ends, without an error.'),
  bullet('**Slow work runs in the background.** Uploads are answered in under a second; a queue feeds a worker that analyzes at most two rooms at a time and resumes unfinished rooms after a restart.'),
  bullet('**The AI is chosen per room.** Each room records whether the fake or the real AI analyzes it. The fake AI is free and instant and replays real recorded results for the sample photos; production always uses the real AI.'),
  bullet('**Nothing is overwritten.** Every outline fix is stored as a new version of the label map; undo and redo just point the room at another version. Database rows carry a version stamp, so two workers can\'t silently overwrite each other; changes to different fields (a room moving to an account while it is analyzed) are merged instead of failing.'),
  bullet('**Only saved rooms are kept.** A room lives inside a design. Removing it from its design deletes its row, photo and outlines; deleting a design deletes its rooms; and a background job deletes uploads that weren\'t saved to a design within 24 hours, so storage only holds what users chose to keep.'),
  bullet('**Accounts without walls.** Visitors try everything as a guest: each browser gets its own private guest id in a cookie. Signing up or in moves that browser\'s rooms into the account, so nothing is lost. Accounts use ASP.NET Core Identity with an HTTP-only, Secure, SameSite=Lax cookie; wrong passwords lock an account for a few minutes. The use cases only see "the current user", so they don\'t know about cookies at all.'),
  bullet('**The AI is tuned in the database.** The Claude model, effort, prompt and limits, and the SAM 3 mask rules, are one row of the `ai_settings` table, read at the start of each analysis. A change applies to the next upload with no rebuild or restart, and values that can\'t work are refused with the reason. The keys stay in secrets; an admin app will edit the row later, pgAdmin does for now.'),
  bullet('**Costs are bounded.** A photo costs about 3 cents: one Claude call and one SAM 3 request (down from about 13 cents with one SAM 3 request per object). Uploads and sign-in attempts are rate-limited, the analysis image is capped at 2048 px, objects are capped at 40, and every analysis logs its Claude tokens and SAM 3 requests so costs can be measured.'),
  bullet('**Every fixed value has one home.** Limits, durations, setting keys, routes, cookie and table names, AI field names and the messages people see live in a `Constants` class per project (in groups such as `Constants.Photos` or `Constants.Messages`), never written inline. Enums live there too, such as `RoomStatus` and `AiMode` in the Domain. The web app keeps its own in `src/lib/constants.ts`, including the enums as constant objects such as `ROOM_STATUS`, and its limits name the API constant they follow. A limit is changed in one place, and a message that mentions a number always matches it.'),

  h1('4. Frontend: a single-page app'),
  p('The web app is organized by feature (upload, rooms, editor, account, designs) rather than by technical type. It keeps three kinds of state apart:'),
  bullet('**Server state** (the account, rooms, their objects, designs and their rooms, AI availability) lives in TanStack Query, which caches, retries and refetches. Live SignalR updates are written straight into that cache, and signing in or out fetches everything again.'),
  bullet('**Editor state** (selection, recolors, the brush, undo history, the open design and what was last saved) lives in one Zustand store. Undo works on snapshots, so one history covers both color changes and outline fixes, and the recolors survive the trip to the sign-in page.'),
  bullet('**Pixels** live in a WebGL renderer outside React. React decides what to show; the renderer draws it at 60 frames per second.'),
  p('The brush follows the same rules in the browser and on the server, written twice in the two languages, so the live preview is exactly what gets saved, pixel for pixel.'),

  h1('5. Why not MVC?'),
  p('MVC (Model-View-Controller) suits apps where the server renders a page for every click: forms, lists, admin screens. DesignApp is mostly a live editor plus slow background AI, which MVC isn\'t built for.'),
  table(['Need', 'Classic server-side MVC', 'What DesignApp does'], [
    ['Recolor and brush at 60 fps', 'Each change is a request and a new page', 'The browser does it alone with WebGL'],
    ['13–20 s AI analysis', 'Blocks the request, or needs extra plumbing', 'Background worker plus live SignalR progress'],
    ['Swap or fake the AI', 'AI calls usually sit in controllers or services', 'Use cases talk to interfaces; adapters are swapped'],
    ['Test without database or AI', 'Hard when logic lives in controllers', '147 unit tests run in memory in about 15 seconds'],
    ['Future mobile app', 'Pages can\'t be reused', 'The same JSON API serves any client'],
  ], [2700, 3400, 3538]),
  p('**The trade-off:** this architecture is heavier. There are more projects and files, a change often touches several layers, and there is more indirection to follow. For a small app with simple forms, MVC or Razor Pages would be the simpler and better choice. Here it pays off because of the real-time editor, the background AI, the swappable providers and the testing.'),

  h1('6. How it is tested'),
  bullet('**Unit tests (147)** run the domain rules, use cases, image processing, the brush, guest ids, the 24-hour cleanup and the AI request handling in memory, with fake AI and fake storage.'),
  bullet('**API tests (38)** start the real API against real Postgres and storage containers, with the fake AI: upload, analysis, live updates, outline fixes and undo, scopes and the AI switch, accounts and guests, designs with several rooms, the 24-hour cleanup, abandoned requests, and two writers saving the same room.'),
  bullet('**Browser tests** drive Firefox against the developer\'s own running app with the five sample room photos: clicking every object, recoloring, undo, compare, zoom, the brush, signing up from the editor, saving rooms into new and existing designs, adding and removing rooms, editing details, abandoning requests mid-load, and phone layouts.'),
  bullet('**Paid AI** is never called by tests. Real runs are done by hand, on purpose, and recorded so the fakes can replay them for free.'),

  h1('7. What comes next'),
  p('Milestone 5 (accounts and saved designs) is done, and it confirmed the design: sign-in replaced the shared guest without changing a single use case, because they only ever asked for "the current user". Natural next steps:'),
  bullet('**Account basics:** email confirmation and password reset, which need an email service.'),
  bullet('**Better previews:** small thumbnails, and design cards that show the recolors applied instead of the original photo.'),
  bullet('**Going live:** hosting the API, database and storage in the cloud, with the real AI and its limits always on.'),
]

const doc = new Document({
  creator: 'DesignApp', title: 'DesignApp: Architecture Overview', styles, numbering,
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1134, right: 1134 } } },
    footers: footer('DesignApp: Architecture Overview'),
    children,
  }],
})
/** The document's bytes. */
module.exports = async () => ({ buffer: await Packer.toBuffer(doc) })
