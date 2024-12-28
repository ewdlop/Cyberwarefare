import smtplib
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText

# SMTP server configuration
smtp_server = 'smtp.example.com'
smtp_port = 587
username = 'your_email@example.com'
password = 'your_password'

# Create a MIME object
msg = MIMEMultipart()
msg['From'] = username
msg['To'] = 'recipient@example.com'
msg['Subject'] = 'Test Email'

# Attach the email body
body = 'This is a test email sent from Python!'
msg.attach(MIMEText(body, 'plain'))

try:
    # Connect to the SMTP server
    server = smtplib.SMTP(smtp_server, smtp_port)
    server.starttls()  # Upgrade the connection to a secure encrypted SSL/TLS connection
    server.login(username, password)
    
    # Send the email
    server.send_message(msg)
    print('Email sent successfully')
    
except Exception as e:
    print(f'Failed to send email: {e}')
    
finally:
    # Logout and close the connection
    server.quit()
