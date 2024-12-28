import poplib
from email.parser import BytesParser

# Connecting to the POP3 server
server = poplib.POP3_SSL('pop.example.com')
server.user('your_email@example.com')
server.pass_('your_password')

# Get the number of messages
num_messages = len(server.list()[1])

for i in range(num_messages):
    # Retrieve the message
    response, lines, octets = server.retr(i + 1)
    msg_data = b'\r\n'.join(lines)
    msg = BytesParser().parsebytes(msg_data)
    
    print('From:', msg['From'])
    print('Subject:', msg['Subject'])
    print('Body:', msg.get_payload(decode=True))
    print('---')

# Logout and close the connection
server.quit()
