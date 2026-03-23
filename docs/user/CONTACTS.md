# Contacts — User Guide

> **Last Updated:** 2026-03-23

---

## Welcome

DotNetCloud Contacts lets you store and manage your contacts, organize them into groups, share them with other users, and sync them with external applications via CardDAV.

---

## Managing Contacts

### Creating a Contact

1. Open **Contacts** from the left sidebar
2. Click **New Contact**
3. Fill in the contact details:
   - **Display Name** (required)
   - **First Name**, **Last Name**, **Middle Name**
   - **Organization**, **Department**, **Job Title**
   - **Email addresses** — add multiple with labels (Work, Home, Other)
   - **Phone numbers** — add multiple with labels (Mobile, Work, Home, Fax, Other)
   - **Addresses** — street, city, region, postal code, country
   - **Birthday**, **Anniversary**
   - **Website**, **Notes**
4. Click **Save**

### Editing a Contact

1. Click on a contact to open it
2. Edit any fields
3. Click **Save**

### Deleting a Contact

1. Select a contact
2. Click **Delete** (or right-click → Delete)
3. The contact is soft-deleted and can be recovered by an administrator

### Searching Contacts

Use the **search bar** to find contacts by name, email, phone number, or organization. Results update as you type.

---

## Contact Groups

Groups help you organize contacts into logical collections (e.g., "Family", "Work", "Board Members").

### Creating a Group

1. In the Contacts sidebar, click **New Group**
2. Enter a group name
3. Click **Create**

### Adding Contacts to a Group

1. Open a contact
2. Select the group(s) to add the contact to
3. Save the contact

Or:

1. Open a group
2. Click **Add Members**
3. Select contacts to add

### Removing Contacts from a Group

1. Open the group
2. Click the remove button next to the contact

Removing a contact from a group does not delete the contact.

---

## Sharing Contacts

You can share individual contacts with other users or teams.

### Share with a User

1. Open the contact you want to share
2. Click **Share**
3. Search for the user
4. Choose a permission level:
   - **Read Only** — the recipient can view the contact but not edit it
   - **Read/Write** — the recipient can view and edit the contact
5. Click **Share**

### Share with a Team

1. Open the contact
2. Click **Share**
3. Select a team
4. Choose a permission level
5. Click **Share**

### Removing a Share

1. Open the shared contact
2. Click **Share** to view active shares
3. Click **Remove** next to the share you want to revoke

Only the contact owner can manage shares and delete the contact.

---

## Importing Contacts

### Import from vCard (.vcf)

DotNetCloud accepts vCard 3.0 format files. Most contact applications (Google Contacts, Apple Contacts, Outlook, Thunderbird) can export to this format.

1. Go to **Contacts**
2. Click **Import**
3. Paste or upload your `.vcf` file content
4. Review the import preview (dry-run)
5. Click **Import** to save the contacts

### Supported vCard Fields

| vCard Field | Mapped To |
|---|---|
| `FN` | Display Name |
| `N` | First Name, Last Name, Middle Name, Prefix, Suffix |
| `ORG` | Organization |
| `TITLE` | Job Title |
| `EMAIL` | Email Address |
| `TEL` | Phone Number |
| `ADR` | Address |
| `BDAY` | Birthday |
| `URL` | Website |
| `NOTE` | Notes |

### Import Tips

- You can import a single vCard or a file containing multiple vCards
- The system detects duplicates by primary email address
- Use **dry-run mode** first to check for errors before importing large files
- If a vCard contains a `PHOTO` property, the avatar is automatically imported and saved to the contact

---

## Exporting Contacts

### Export All Contacts

1. Go to **Contacts**
2. Click **Export**
3. All your contacts download as a combined `.vcf` file

### Export a Single Contact

1. Open a contact
2. Click **Export as vCard**
3. The contact downloads as a `.vcf` file

Exported vCards use version 3.0 format compatible with all major contact applications. If a contact has an avatar, it is included in the vCard as a `PHOTO` property.

---

## Contact Avatars

You can add a profile photo to any contact.

### Uploading an Avatar

1. Open a contact
2. Click the avatar area (or the **Change Photo** button)
3. Select an image file (JPEG, PNG, GIF, WebP, or SVG)
4. The avatar is saved immediately

**Limits:** Maximum file size is 5 MB.

### Removing an Avatar

1. Open the contact
2. Click the avatar area
3. Click **Remove Photo**

### Avatar & vCard Sync

- When you export a contact as vCard, the avatar is embedded as a `PHOTO` property
- When you import a vCard with a `PHOTO` property, the avatar is automatically saved
- CardDAV clients (DAVx5, Thunderbird, iOS/macOS) sync avatars automatically

---

## Contact Attachments

You can attach files to a contact for reference (documents, contracts, business cards, etc.).

### Adding an Attachment

1. Open a contact
2. Scroll to the **Attachments** section
3. Click **Add Attachment**
4. Select a file (maximum 25 MB)
5. Optionally add a description

### Viewing Attachments

Attachments are listed in the contact detail view with file name, size, and date. Click an attachment to download it.

### Removing an Attachment

1. Open the contact
2. In the **Attachments** section, click the delete button next to the attachment

Only the contact owner can add or remove attachments.

---

## CardDAV Sync

CardDAV lets you sync your DotNetCloud contacts with external applications in real time.

### Setting Up DAVx5 (Android)

1. Install **DAVx5** from Google Play or F-Droid
2. Add a new account → **Login with URL**
3. Enter your server URL: `https://your-server/.well-known/carddav`
4. Authenticate with your DotNetCloud credentials
5. Select the address book to sync
6. Contacts will appear in your phone's Contacts app

### Setting Up Thunderbird

1. Install the **CardBook** add-on
2. Add a new address book → **Remote** → **CardDAV**
3. URL: `https://your-server/.well-known/carddav`
4. Enter your credentials
5. Contacts sync automatically

### Setting Up iOS / macOS

1. Go to **Settings** → **Accounts** → **Add Account** → **Other**
2. Select **Add CardDAV Account**
3. Server: `your-server`
4. User Name and Password: your DotNetCloud credentials
5. Contacts appear in the Contacts app

### Sync Behavior

- Changes made on any device are synced automatically
- Conflict detection uses ETags — the most recent change wins
- Deleted contacts are removed from all synced devices
- New contacts created on external devices appear in DotNetCloud

---

## Troubleshooting

### Contact Not Appearing After Import

- Check the import report for errors or skipped records
- Verify the vCard file uses version 3.0 format
- Ensure the file is UTF-8 encoded

### CardDAV Sync Not Working

- Verify the server URL includes `/.well-known/carddav`
- Check that your authentication credentials are correct
- Ensure the server's TLS certificate is trusted by your device

### Shared Contact Not Visible

- Ask the owner to verify the share is active
- Check that you're logged in as the correct user
- Shares appear in your contact list alongside owned contacts
