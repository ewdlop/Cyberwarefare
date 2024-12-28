import imaplib
import email

# Connecting to the IMAP server
mail = imaplib.IMAP4_SSL('imap.example.com')
mail.login('your_email@example.com', 'your_password')

# Select the mailbox you want to use (e.g., INBOX)
mail.select('inbox')

# Search for all emails in the mailbox
status, messages = mail.search(None, 'ALL')

# Convert messages to a list of email IDs
email_ids = messages[0].split()

for email_id in email_ids:
    status, msg_data = mail.fetch(email_id, '(RFC822)')
    msg = email.message_from_bytes(msg_data[0][1])
    print('From:', msg['From'])
    print('Subject:', msg['Subject'])
    print('Body:', msg.get_payload(decode=True))
    print('---')

# Logout and close the connection
mail.logout()
