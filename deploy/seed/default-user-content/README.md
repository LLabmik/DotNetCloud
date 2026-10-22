# Default user content

This tree is the stock starting layout for every new DotNetCloud account.

It is copied **once** into the deployment's data directory
(`${DOTNETCLOUD_DATA_DIR}/default-user-content`) on a fresh install, and is never copied over an
existing template again. Upgrades refresh this packaged copy but leave the data-directory template
untouched.

- `Documents/`, `Photos/`, `Music/`, `Templates/`, `Videos/` — the guaranteed stock folders.
- Add starter documents, images or templates here if you build your own release.
- The administrator can add or remove files directly in the data-directory template at any time;
  changes converge on the next start-up or daily pass.
- Users receive read-only copies that do not count against their quota and keep no version history.
