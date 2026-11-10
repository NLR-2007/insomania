// ─────────────────────────────────────────────────────────────────────────────
//  EDIT THIS FILE TO POINT THE DOWNLOAD BUTTON AT YOUR FILE
// ─────────────────────────────────────────────────────────────────────────────
//
//  Put Insominia.exe in Google Drive, then:
//
//    1. Right-click the file  →  Share  →  General access
//       →  "Anyone with the link"  (otherwise visitors get a sign-in wall)
//
//    2. Copy the link. It looks like:
//         https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUv/view?usp=sharing
//                                        ^^^^^^^^^^^^^^^^^^^^^^^^  this is the FILE ID
//
//    3. Paste JUST THE FILE ID below.
//
//  Using the file id (rather than the /view link) gives a direct download
//  instead of a Drive preview page the visitor has to click through.
//
export const DRIVE_FILE_ID = 'PASTE_YOUR_GOOGLE_DRIVE_FILE_ID_HERE'

// Drive's direct-download endpoint.
export const DOWNLOAD_URL =
  DRIVE_FILE_ID === 'PASTE_YOUR_GOOGLE_DRIVE_FILE_ID_HERE'
    ? null
    : `https://drive.google.com/uc?export=download&id=${DRIVE_FILE_ID}`

export const RELEASE = {
  version: '2.0.0',
  size: '139 MB',
  date: 'November 2026',
  sha256: 'D18A3E13AE44BDE55173098D442E5B5DFCC870107CEEB4B0EA92734A651E419B',

  // Published self-contained: .NET is bundled inside the exe, so there is
  // nothing for the visitor to install first. That is what the extra size
  // buys. Setting this to an object again would re-show a requirement notice.
  runtime: null,
}

export const REPO_URL = 'https://github.com/'
